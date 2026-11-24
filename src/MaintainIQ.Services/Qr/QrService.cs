using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using MaintainIQ.Domain.Common;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using QRCoder;

namespace MaintainIQ.Services.Qr;

/// <summary>Yazdırılabilir ekipman etiketi: QR PNG + aynı içeriği taşıyan vektörel SVG etiket.</summary>
public sealed record QrLabel(
    int EquipmentId,
    string EquipmentCode,
    string EquipmentName,
    string Payload,
    byte[] QrPng,
    string LabelSvg);

public interface IQrService
{
    string BuildPayload(string qrToken);
    bool TryParsePayload(string? scannedText, out string qrToken);
    byte[] GeneratePng(string payload, int pixelsPerModule = 10);
    Task<QrLabel> CreateLabelAsync(int equipmentId, CancellationToken cancellationToken = default);
    Task<Domain.Entities.Equipment> ResolveAsync(string scannedText, CancellationToken cancellationToken = default);
}

public sealed partial class QrService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock) : IQrService
{
    public const string PayloadPrefix = "MAINTAINIQ:EQ:";
    private const int MaxTokenLength = 100;
    private readonly AuditWriter _audit = new(currentUser);

    public string BuildPayload(string qrToken)
    {
        if (!IsValidToken(qrToken))
        {
            throw new DomainValidationException("QR token yalnızca harf, rakam, '-' ve '_' içerebilir (en fazla 100 karakter).");
        }

        return PayloadPrefix + qrToken;
    }

    public bool TryParsePayload(string? scannedText, out string qrToken)
    {
        qrToken = string.Empty;
        var text = scannedText?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith(PayloadPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = text[PayloadPrefix.Length..];
        if (!IsValidToken(candidate))
        {
            return false;
        }

        qrToken = candidate;
        return true;
    }

    public byte[] GeneratePng(string payload, int pixelsPerModule = 10)
    {
        if (pixelsPerModule is < 2 or > 40)
        {
            throw new DomainValidationException("QR modül boyutu 2 ile 40 piksel arasında olmalıdır.");
        }

        using var data = CreateData(payload);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    public async Task<QrLabel> CreateLabelAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var equipment = await ServiceTransaction.RunAsync(sessionFactory, async session =>
        {
            var item = await session.Equipment.GetByIdAsync(equipmentId, cancellationToken).ConfigureAwait(false);
            if (item is null || item.IsDeleted)
            {
                throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), equipmentId);
            }

            if (item.QrToken is null)
            {
                permissions.Ensure(Permission.ManageEquipment);
                var now = clock.UtcNow;
                item.SetQrToken(Guid.NewGuid().ToString("N"), now);
                await session.Equipment.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
                await _audit.WriteAsync(
                    session, nameof(Domain.Entities.Equipment), item.Id.ToString(CultureInfo.InvariantCulture),
                    "QrTokenAssigned", null, new { item.QrToken }, now, cancellationToken).ConfigureAwait(false);
            }

            return item;
        }, cancellationToken).ConfigureAwait(false);

        var payload = BuildPayload(equipment.QrToken!);
        using var data = CreateData(payload);
        var png = new PngByteQRCode(data).GetGraphic(10);
        return new QrLabel(
            equipment.Id, equipment.Code, equipment.Name, payload, png,
            BuildLabelSvg(data, equipment.Code, equipment.Name));
    }

    public async Task<Domain.Entities.Equipment> ResolveAsync(string scannedText, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        if (!TryParsePayload(scannedText, out var token))
        {
            throw new DomainValidationException("Okutulan kod bir MaintainIQ ekipman etiketi değil.");
        }

        return await ServiceTransaction.RunAsync(sessionFactory, async session =>
            await session.Equipment.GetByQrTokenAsync(token, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainValidationException("Bu QR koduna bağlı aktif ekipman bulunamadı."),
            cancellationToken).ConfigureAwait(false);
    }

    internal static string BuildLabelSvg(QRCodeData data, string code, string name)
    {
        const int moduleSize = 6;
        const int padding = 12;
        var modules = data.ModuleMatrix;
        var qrSize = modules.Count * moduleSize;
        var width = qrSize + (padding * 2);
        var height = qrSize + (padding * 2) + 48;
        var svg = new StringBuilder()
            .Append(CultureInfo.InvariantCulture,
                $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">")
            .Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/>")
            .Append("<path fill=\"#000000\" d=\"");
        for (var y = 0; y < modules.Count; y++)
        {
            for (var x = 0; x < modules[y].Count; x++)
            {
                if (modules[y][x])
                {
                    svg.Append(CultureInfo.InvariantCulture,
                        $"M{padding + (x * moduleSize)} {padding + (y * moduleSize)}h{moduleSize}v{moduleSize}h-{moduleSize}z");
                }
            }
        }

        var textX = width / 2;
        return svg
            .Append("\"/>")
            .Append(CultureInfo.InvariantCulture,
                $"<text x=\"{textX}\" y=\"{qrSize + padding + 22}\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"16\" font-weight=\"700\">{SecurityElement.Escape(code)}</text>")
            .Append(CultureInfo.InvariantCulture,
                $"<text x=\"{textX}\" y=\"{qrSize + padding + 42}\" text-anchor=\"middle\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"12\">{SecurityElement.Escape(name)}</text>")
            .Append("</svg>")
            .ToString();
    }

    private static QRCodeData CreateData(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new DomainValidationException("QR içeriği boş olamaz.");
        }

        using var generator = new QRCodeGenerator();
        return generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
    }

    private static bool IsValidToken(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxTokenLength && TokenPattern().IsMatch(token);

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new PermissionDeniedException("Bu işlem için oturum açılmalıdır.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
