using System.Globalization;
using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using SkiaSharp;

namespace MaintainIQ.Services.Attachments;

public sealed class AttachmentOptions
{
    /// <summary>Dosyaların saklandığı klasör (varsayılan: uygulama klasörü altında App_Data/attachments).</summary>
    public string RootDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "attachments");

    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;

    public int ThumbnailMaxPixels { get; init; } = 200;

    public int MaxImageDimension { get; init; } = 8000;
}

public sealed record AttachmentUpload(AttachmentOwnerType OwnerType, int OwnerId, string FileName, Stream Content);

public interface IAttachmentService
{
    Task<int> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Attachment>> GetForOwnerAsync(AttachmentOwnerType ownerType, int ownerId, CancellationToken cancellationToken = default);
    Task<Stream> OpenAsync(int attachmentId, CancellationToken cancellationToken = default);
    Task<Stream> OpenThumbnailAsync(int attachmentId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int attachmentId, CancellationToken cancellationToken = default);
    Task<int> DeleteForOwnerAsync(AttachmentOwnerType ownerType, int ownerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ekipman/arıza/iş emri fotoğrafları. Dosya App_Data/attachments altında GUID adla, meta veri DB'de tutulur.
/// Yükleme: önce dosya + küçük resim yazılır, sonra DB transaction'ı; DB başarısızsa dosyalar silinir.
/// Silme: önce DB transaction'ı, commit sonrası dosyalar silinir (yetim kayıt yerine yetim dosya riski seçildi).
/// </summary>
public sealed class AttachmentService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock,
    AttachmentOptions options) : IAttachmentService
{
    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private readonly AuditWriter _audit = new(currentUser);

    public static string ThumbnailNameFor(string storedFileName) =>
        Path.GetFileNameWithoutExtension(storedFileName) + "_thumb.png";

    public async Task<int> UploadAsync(AttachmentUpload upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        permissions.Ensure(PermissionFor(upload.OwnerType));
        var originalName = Path.GetFileName(upload.FileName?.Trim() ?? string.Empty);
        var extension = Path.GetExtension(originalName);
        if (!AllowedExtensions.TryGetValue(extension, out var contentType))
        {
            throw new DomainValidationException("Yalnızca .jpg, .jpeg ve .png dosyaları yüklenebilir.");
        }

        var bytes = await ReadLimitedAsync(upload.Content, options.MaxFileSizeBytes, cancellationToken).ConfigureAwait(false);
        ValidateSignature(bytes, contentType);
        var thumbnail = CreateThumbnail(bytes, contentType);

        var storedName = Guid.NewGuid().ToString("N") + (contentType == "image/png" ? ".png" : ".jpg");
        var thumbnailName = ThumbnailNameFor(storedName);
        Directory.CreateDirectory(options.RootDirectory);
        var storedPath = PathFor(storedName);
        var thumbnailPath = PathFor(thumbnailName);
        try
        {
            await File.WriteAllBytesAsync(storedPath, bytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(thumbnailPath, thumbnail, cancellationToken).ConfigureAwait(false);
            return await ServiceTransaction.RunAsync(sessionFactory, async session =>
            {
                await EnsureOwnerExistsAsync(session, upload.OwnerType, upload.OwnerId, cancellationToken).ConfigureAwait(false);
                var now = clock.UtcNow;
                var attachment = new Attachment(
                    upload.OwnerType, upload.OwnerId, storedName, originalName, contentType, bytes.LongLength, now);
                var id = await session.Attachments.AddAsync(attachment, cancellationToken).ConfigureAwait(false);
                await _audit.WriteAsync(
                    session, nameof(Attachment), id.ToString(CultureInfo.InvariantCulture), "Uploaded", null,
                    new { attachment.OwnerType, attachment.OwnerId, attachment.OriginalFileName, attachment.SizeBytes },
                    now, cancellationToken).ConfigureAwait(false);
                return id;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(storedPath);
            TryDelete(thumbnailPath);
            throw;
        }
    }

    public Task<IReadOnlyList<Attachment>> GetForOwnerAsync(
        AttachmentOwnerType ownerType,
        int ownerId,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        return ServiceTransaction.RunAsync(
            sessionFactory,
            session => session.Attachments.GetForOwnerAsync((byte)ownerType, ownerId, cancellationToken),
            cancellationToken);
    }

    public async Task<Stream> OpenAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await RequireReadableAsync(attachmentId, cancellationToken).ConfigureAwait(false);
        return OpenFile(attachment.StoredFileName);
    }

    public async Task<Stream> OpenThumbnailAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await RequireReadableAsync(attachmentId, cancellationToken).ConfigureAwait(false);
        return OpenFile(ThumbnailNameFor(attachment.StoredFileName));
    }

    public async Task DeleteAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        var storedName = await ServiceTransaction.RunAsync(sessionFactory, async session =>
        {
            var attachment = await session.Attachments.GetByIdAsync(attachmentId, cancellationToken).ConfigureAwait(false)
                ?? throw new EntityNotFoundException(nameof(Attachment), attachmentId);
            permissions.Ensure(PermissionFor(attachment.OwnerType));
            await DeleteRecordAsync(session, attachment, cancellationToken).ConfigureAwait(false);
            return attachment.StoredFileName;
        }, cancellationToken).ConfigureAwait(false);
        DeleteFiles(storedName);
    }

    public async Task<int> DeleteForOwnerAsync(
        AttachmentOwnerType ownerType,
        int ownerId,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(PermissionFor(ownerType));
        var storedNames = await ServiceTransaction.RunAsync(sessionFactory, async session =>
        {
            var attachments = await session.Attachments.GetForOwnerAsync((byte)ownerType, ownerId, cancellationToken).ConfigureAwait(false);
            foreach (var attachment in attachments)
            {
                await DeleteRecordAsync(session, attachment, cancellationToken).ConfigureAwait(false);
            }

            return attachments.Select(item => item.StoredFileName).ToArray();
        }, cancellationToken).ConfigureAwait(false);
        foreach (var name in storedNames)
        {
            DeleteFiles(name);
        }

        return storedNames.Length;
    }

    private async Task DeleteRecordAsync(IServiceDataSession session, Attachment attachment, CancellationToken cancellationToken)
    {
        await session.Attachments.DeleteAsync(attachment, cancellationToken).ConfigureAwait(false);
        await _audit.WriteAsync(
            session, nameof(Attachment), attachment.Id.ToString(CultureInfo.InvariantCulture), "Deleted",
            new { attachment.OwnerType, attachment.OwnerId, attachment.OriginalFileName, attachment.StoredFileName },
            null, clock.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Attachment> RequireReadableAsync(int attachmentId, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        return await ServiceTransaction.RunAsync(sessionFactory, async session =>
            await session.Attachments.GetByIdAsync(attachmentId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException(nameof(Attachment), attachmentId),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureOwnerExistsAsync(
        IServiceDataSession session,
        AttachmentOwnerType ownerType,
        int ownerId,
        CancellationToken cancellationToken)
    {
        var exists = ownerType switch
        {
            AttachmentOwnerType.Equipment =>
                await session.Equipment.GetByIdAsync(ownerId, cancellationToken).ConfigureAwait(false) is { IsDeleted: false },
            AttachmentOwnerType.Failure =>
                await session.Failures.GetByIdAsync(ownerId, cancellationToken).ConfigureAwait(false) is not null,
            AttachmentOwnerType.WorkOrder =>
                await session.WorkOrders.GetByIdAsync(ownerId, cancellationToken).ConfigureAwait(false) is not null,
            _ => throw new DomainValidationException("Bilinmeyen ek sahibi türü."),
        };
        if (!exists)
        {
            throw new EntityNotFoundException(ownerType.ToString(), ownerId);
        }
    }

    private static Permission PermissionFor(AttachmentOwnerType ownerType) => ownerType switch
    {
        AttachmentOwnerType.Equipment => Permission.ManageEquipment,
        AttachmentOwnerType.Failure => Permission.ReportFailure,
        AttachmentOwnerType.WorkOrder => Permission.ExecuteOwnWorkOrder,
        _ => throw new DomainValidationException("Bilinmeyen ek sahibi türü."),
    };

    private static async Task<byte[]> ReadLimitedAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new DomainValidationException(
                    $"Dosya boyutu en fazla {maxBytes / (1024 * 1024)} MB olabilir.");
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            throw new DomainValidationException("Boş dosya yüklenemez.");
        }

        return buffer.ToArray();
    }

    private static void ValidateSignature(byte[] bytes, string contentType)
    {
        var signature = contentType == "image/png" ? PngSignature : JpegSignature;
        if (bytes.Length < signature.Length || !bytes.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            throw new DomainValidationException("Dosya içeriği uzantısıyla uyuşmuyor.");
        }
    }

    private byte[] CreateThumbnail(byte[] bytes, string contentType)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(input)
            ?? throw new DomainValidationException("Görüntü dosyası okunamadı veya bozuk.");
        var expected = contentType == "image/png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        if (codec.EncodedFormat != expected)
        {
            throw new DomainValidationException("Dosya içeriği uzantısıyla uyuşmuyor.");
        }

        var info = codec.Info;
        if (info.Width > options.MaxImageDimension || info.Height > options.MaxImageDimension)
        {
            throw new DomainValidationException(
                $"Görüntü en fazla {options.MaxImageDimension} x {options.MaxImageDimension} piksel olabilir.");
        }

        using var bitmap = SKBitmap.Decode(codec)
            ?? throw new DomainValidationException("Görüntü dosyası okunamadı veya bozuk.");
        var scale = Math.Min(1d, Math.Min(
            (double)options.ThumbnailMaxPixels / bitmap.Width,
            (double)options.ThumbnailMaxPixels / bitmap.Height));
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        using var resized = bitmap.Resize(
            new SKImageInfo(width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new DomainValidationException("Küçük resim oluşturulamadı.");
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private FileStream OpenFile(string fileName)
    {
        var path = PathFor(fileName);
        if (!File.Exists(path))
        {
            throw new DomainValidationException("Ek dosyası diskte bulunamadı.");
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    private void DeleteFiles(string storedName)
    {
        TryDelete(PathFor(storedName));
        TryDelete(PathFor(ThumbnailNameFor(storedName)));
    }

    private string PathFor(string fileName)
    {
        var root = Path.GetFullPath(options.RootDirectory);
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("Geçersiz ek dosyası yolu.");
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Dosya kilitli veya kaybolmuş olabilir; DB tutarlılığı önceliklidir.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new PermissionDeniedException("Bu işlem için oturum açılmalıdır.");
        }
    }
}
