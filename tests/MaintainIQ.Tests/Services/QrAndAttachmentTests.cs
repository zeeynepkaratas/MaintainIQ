using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Analytics;
using MaintainIQ.Services.Attachments;
using MaintainIQ.Services.Qr;
using NSubstitute;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace MaintainIQ.Tests.Services;

public sealed class QrServiceTests
{
    [Fact]
    public void GeneratedPng_DecodesBackToSameTokenAndEquipment()
    {
        var service = Service(new ServiceFixture());
        var token = Guid.NewGuid().ToString("N");

        var png = service.GeneratePng(service.BuildPayload(token));
        var decoded = Decode(png);

        Assert.Equal("MAINTAINIQ:EQ:" + token, decoded);
        Assert.True(service.TryParsePayload(decoded, out var parsed));
        Assert.Equal(token, parsed);
    }

    [Theory]
    [InlineData("maintainiq:eq:ABC-123_x", true)]
    [InlineData("  MAINTAINIQ:EQ:abc  ", true)]
    [InlineData("MAINTAINIQ:EQ:", false)]
    [InlineData("MAINTAINIQ:WO:abc", false)]
    [InlineData("MAINTAINIQ:EQ:abc def", false)]
    [InlineData("https://example.com", false)]
    [InlineData(null, false)]
    public void TryParsePayload_AcceptsOnlyEquipmentLabels(string? text, bool expected)
    {
        Assert.Equal(expected, Service(new ServiceFixture()).TryParsePayload(text, out _));
    }

    [Fact]
    public void BuildPayloadAndPng_RejectInvalidInput()
    {
        var service = Service(new ServiceFixture());

        Assert.Throws<DomainValidationException>(() => service.BuildPayload("bad token"));
        Assert.Throws<DomainValidationException>(() => service.BuildPayload(new string('a', 101)));
        Assert.Throws<DomainValidationException>(() => service.GeneratePng("x", 1));
        Assert.Throws<DomainValidationException>(() => service.GeneratePng(" "));
    }

    [Fact]
    public async Task CreateLabel_AssignsMissingTokenAndReturnsPrintableLabel()
    {
        var fixture = new ServiceFixture();
        var equipment = Equipment.Rehydrate(
            5, null, "EQ-5", "Pres <A&B>", "Hat 1", null, EquipmentStatus.Operational, false,
            ServiceFixture.Now.AddDays(-1), ServiceFixture.Now.AddDays(-1), [1]);
        fixture.Equipment.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(equipment);

        var label = await Service(fixture).CreateLabelAsync(5);

        Assert.NotNull(equipment.QrToken);
        Assert.Equal("MAINTAINIQ:EQ:" + equipment.QrToken, label.Payload);
        Assert.Equal(label.Payload, Decode(label.QrPng));
        Assert.Contains("EQ-5", label.LabelSvg, StringComparison.Ordinal);
        Assert.Contains("Pres &lt;A&amp;B&gt;", label.LabelSvg, StringComparison.Ordinal);
        Assert.StartsWith("<svg", label.LabelSvg, StringComparison.Ordinal);
        await fixture.Equipment.Received(1).UpdateAsync(equipment, Arg.Any<CancellationToken>());
        await fixture.Audit.Received(1).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLabel_WithExistingToken_DoesNotWrite_AndOperatorCanPrint()
    {
        var fixture = new ServiceFixture();
        fixture.CurrentUser.SignIn(2, 8, "operator", UserRole.Operator);
        var equipment = Equipment.Rehydrate(
            5, null, "EQ-5", "Pres", "Hat 1", "tok123", EquipmentStatus.Operational, false,
            ServiceFixture.Now, ServiceFixture.Now, [1]);
        fixture.Equipment.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(equipment);

        var label = await Service(fixture).CreateLabelAsync(5);

        Assert.Equal("MAINTAINIQ:EQ:tok123", label.Payload);
        await fixture.Equipment.DidNotReceive().UpdateAsync(Arg.Any<Equipment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLabel_ForMissingEquipment_Throws()
    {
        await Assert.ThrowsAsync<MaintainIQ.Services.Common.EntityNotFoundException>(() =>
            Service(new ServiceFixture()).CreateLabelAsync(404));
    }

    [Fact]
    public async Task Resolve_MapsScannedTextToEquipment()
    {
        var fixture = new ServiceFixture();
        var equipment = new Equipment("EQ-7", "Fan", "Çatı", ServiceFixture.Now, "tok7");
        fixture.Equipment.GetByQrTokenAsync("tok7", Arg.Any<CancellationToken>()).Returns(equipment);
        var service = Service(fixture);

        Assert.Same(equipment, await service.ResolveAsync(" MAINTAINIQ:EQ:tok7 "));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ResolveAsync("MAINTAINIQ:EQ:unknown"));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ResolveAsync("hello"));
        fixture.CurrentUser.SignOut();
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.ResolveAsync("MAINTAINIQ:EQ:tok7"));
    }

    internal static string Decode(byte[] png)
    {
        using var decoded = SKBitmap.Decode(png);
        using var bitmap = decoded.Copy(SKColorType.Bgra8888);
        var source = new RGBLuminanceSource(bitmap.Bytes, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var result = new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)));
        return Assert.IsType<Result>(result).Text;
    }

    private static QrService Service(ServiceFixture fixture) =>
        new(fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock);
}

public sealed class AttachmentServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "maintainiq-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_StoresOriginalAndThumbnailAndMetadata()
    {
        var fixture = OwnerFixture();
        var service = Service(fixture);
        Attachment? stored = null;
        fixture.Attachments.AddAsync(Arg.Do<Attachment>(a => stored = a), Arg.Any<CancellationToken>()).Returns(109);

        var id = await service.UploadAsync(Upload("Pompa.PNG", Png(800, 400)));

        Assert.Equal(109, id);
        Assert.NotNull(stored);
        Assert.Equal("Pompa.PNG", stored.OriginalFileName);
        Assert.Equal("image/png", stored.ContentType);
        Assert.True(File.Exists(Path.Combine(_root, stored.StoredFileName)));
        var thumbnailPath = Path.Combine(_root, AttachmentService.ThumbnailNameFor(stored.StoredFileName));
        using var thumbnail = SKBitmap.Decode(thumbnailPath);
        Assert.Equal(200, thumbnail.Width);
        Assert.Equal(100, thumbnail.Height);
        await fixture.Audit.Received(1).AddAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_Jpeg_IsAccepted_AndSmallImageIsNotUpscaled()
    {
        var fixture = OwnerFixture();
        Attachment? stored = null;
        fixture.Attachments.AddAsync(Arg.Do<Attachment>(a => stored = a), Arg.Any<CancellationToken>()).Returns(109);

        await Service(fixture).UploadAsync(Upload("ariza.jpeg", Encode(Bitmap(50, 80), SKEncodedImageFormat.Jpeg)));

        Assert.Equal("image/jpeg", stored!.ContentType);
        Assert.EndsWith(".jpg", stored.StoredFileName, StringComparison.Ordinal);
        using var thumbnail = SKBitmap.Decode(Path.Combine(_root, AttachmentService.ThumbnailNameFor(stored.StoredFileName)));
        Assert.Equal(50, thumbnail.Width);
    }

    [Theory]
    [InlineData("belge.pdf")]
    [InlineData("resim.gif")]
    [InlineData("uzantisiz")]
    public async Task Upload_RejectsUnsupportedExtensions(string fileName)
    {
        var fixture = OwnerFixture();

        await Assert.ThrowsAsync<DomainValidationException>(() => Service(fixture).UploadAsync(Upload(fileName, Png(10, 10))));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task Upload_RejectsContentThatDoesNotMatchExtension()
    {
        var fixture = OwnerFixture();
        var service = Service(fixture);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.UploadAsync(Upload("sahte.png", "not an image"u8.ToArray())));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.UploadAsync(Upload("sahte.jpg", Png(10, 10))));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.UploadAsync(Upload("bos.png", [])));
        await fixture.Attachments.DidNotReceive().AddAsync(Arg.Any<Attachment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_RejectsTooLargeFilesAndDimensions()
    {
        var fixture = OwnerFixture();
        var small = new AttachmentService(fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock,
            new AttachmentOptions { RootDirectory = _root, MaxFileSizeBytes = 100 });
        var narrow = new AttachmentService(fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock,
            new AttachmentOptions { RootDirectory = _root, MaxImageDimension = 100 });

        await Assert.ThrowsAsync<DomainValidationException>(() => small.UploadAsync(Upload("buyuk.png", Png(400, 400))));
        await Assert.ThrowsAsync<DomainValidationException>(() => narrow.UploadAsync(Upload("genis.png", Png(101, 10))));
    }

    [Fact]
    public async Task Upload_WhenDatabaseFails_RemovesWrittenFiles()
    {
        var fixture = OwnerFixture();
        fixture.Attachments.AddAsync(Arg.Any<Attachment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("db down")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(fixture).UploadAsync(Upload("p.png", Png(20, 20))));

        Assert.Empty(Directory.GetFiles(_root));
        await fixture.UnitOfWork.Received(1).RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Upload_ForMissingOwner_FailsAndCleansUp()
    {
        var fixture = new ServiceFixture();

        await Assert.ThrowsAsync<MaintainIQ.Services.Common.EntityNotFoundException>(() =>
            Service(fixture).UploadAsync(new AttachmentUpload(AttachmentOwnerType.Failure, 9, "p.png", new MemoryStream(Png(20, 20)))));

        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task Upload_RequiresOwnerSpecificPermission()
    {
        var fixture = OwnerFixture();
        fixture.CurrentUser.SignIn(2, 8, "operator", UserRole.Operator);

        await Assert.ThrowsAsync<PermissionDeniedException>(() => Service(fixture).UploadAsync(Upload("p.png", Png(10, 10))));
    }

    [Fact]
    public async Task Delete_RemovesRecordThenFiles_AndOpenReadsThem()
    {
        var fixture = OwnerFixture();
        Attachment? stored = null;
        fixture.Attachments.AddAsync(Arg.Do<Attachment>(a => stored = a), Arg.Any<CancellationToken>()).Returns(109);
        var service = Service(fixture);
        await service.UploadAsync(Upload("p.png", Png(30, 30)));
        fixture.Attachments.GetByIdAsync(109, Arg.Any<CancellationToken>()).Returns(stored);

        await using (var original = await service.OpenAsync(109))
        await using (var thumbnail = await service.OpenThumbnailAsync(109))
        {
            Assert.True(original.Length > 0);
            Assert.True(thumbnail.Length > 0);
        }

        await service.DeleteAsync(109);

        Assert.Empty(Directory.GetFiles(_root));
        await fixture.Attachments.Received(1).DeleteAsync(stored!, Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<DomainValidationException>(() => service.OpenAsync(109));
        await Assert.ThrowsAsync<MaintainIQ.Services.Common.EntityNotFoundException>(() => service.DeleteAsync(110));
    }

    [Fact]
    public async Task DeleteForOwner_RemovesAllOwnerAttachments()
    {
        var fixture = OwnerFixture();
        var stored = new List<Attachment>();
        fixture.Attachments.AddAsync(Arg.Do<Attachment>(stored.Add), Arg.Any<CancellationToken>()).Returns(109);
        var service = Service(fixture);
        await service.UploadAsync(Upload("a.png", Png(10, 10)));
        await service.UploadAsync(Upload("b.png", Png(10, 10)));
        fixture.Attachments.GetForOwnerAsync((byte)AttachmentOwnerType.Equipment, 5, Arg.Any<CancellationToken>()).Returns(stored);

        Assert.Equal(2, (await service.GetForOwnerAsync(AttachmentOwnerType.Equipment, 5)).Count);
        var deleted = await service.DeleteForOwnerAsync(AttachmentOwnerType.Equipment, 5);

        Assert.Equal(2, deleted);
        Assert.Empty(Directory.GetFiles(_root));
    }

    private ServiceFixture OwnerFixture()
    {
        var fixture = new ServiceFixture();
        fixture.Equipment.GetByIdAsync(5, Arg.Any<CancellationToken>())
            .Returns(new Equipment("EQ-5", "Pompa", "Hat 1", ServiceFixture.Now));
        return fixture;
    }

    private AttachmentService Service(ServiceFixture fixture) => new(
        fixture.Factory, fixture.CurrentUser, fixture.Permissions, fixture.Clock,
        new AttachmentOptions { RootDirectory = _root });

    private static AttachmentUpload Upload(string fileName, byte[] content) =>
        new(AttachmentOwnerType.Equipment, 5, fileName, new MemoryStream(content));

    private static byte[] Png(int width, int height) => Encode(Bitmap(width, height), SKEncodedImageFormat.Png);

    private static SKBitmap Bitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(30, 120, 200));
        return bitmap;
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using (bitmap)
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(format, 90))
        {
            return data.ToArray();
        }
    }
}

public sealed class KpiServiceTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dashboard_CombinesSummaryAndSqlDatasets_WithLaborCost()
    {
        var fixture = new ServiceFixture();
        var reports = Substitute.For<IReportQueries>();
        var scope = AnalyticsScope.Subtree(1);
        reports.GetKpiFactsAsync(scope, From, To, Arg.Any<CancellationToken>()).Returns(KpiCalculatorTests.SampleFacts());
        reports.GetFailureParetoAsync(scope, From, To, Arg.Any<CancellationToken>())
            .Returns([new FailureParetoItemDto { CauseCode = "MECHANICAL", FailureCount = 3, CumulativePercent = 100 }]);
        reports.GetEquipmentCostsAsync(scope, From, To, Arg.Any<CancellationToken>()).Returns(
        [
            new EquipmentCostDto { EquipmentId = 1, Code = "EQ-1", PartsCost = 350m, LaborMinutes = 150, ClosedWorkOrders = 2 },
            new EquipmentCostDto { EquipmentId = 2, Code = "EQ-2", PartsCost = 0m, LaborMinutes = 0 },
        ]);
        reports.GetCriticalStockCountAsync(Arg.Any<CancellationToken>()).Returns(4);
        var afterPeriod = new ServiceFixture.FixedClock(new DateTimeOffset(2026, 12, 5, 0, 0, 0, TimeSpan.Zero));
        var service = new KpiService(reports, fixture.Permissions, afterPeriod, new KpiOptions());

        var dashboard = await service.GetDashboardAsync(scope, From, To);

        Assert.Equal(scope, dashboard.Scope);
        Assert.Equal(468m, dashboard.Summary.MtbfHours);
        Assert.Single(dashboard.FailurePareto);
        Assert.Equal(4, dashboard.CriticalStockCount);
        var first = dashboard.EquipmentCosts[0];
        Assert.Equal("EQ-1", first.Code);
        Assert.Equal(1250m, first.LaborCost);
        Assert.Equal(1600m, first.TotalCost);
    }

    [Fact]
    public async Task Summary_ValidatesPeriodAndPermission()
    {
        var fixture = new ServiceFixture();
        var service = new KpiService(Substitute.For<IReportQueries>(), fixture.Permissions, fixture.Clock, new KpiOptions());

        await Assert.ThrowsAsync<DomainValidationException>(() => service.GetSummaryAsync(AnalyticsScope.Plant, To, From));
        fixture.CurrentUser.SignOut();
        await Assert.ThrowsAsync<PermissionDeniedException>(() => service.GetSummaryAsync(AnalyticsScope.Plant, From, To));
    }

    [Fact]
    public void Scope_Factories_DescribeSubtreeAndSingleEquipment()
    {
        Assert.Equal(new AnalyticsScope(null, true), AnalyticsScope.Plant);
        Assert.True(AnalyticsScope.Subtree(4).IncludeDescendants);
        Assert.False(AnalyticsScope.Single(4).IncludeDescendants);
        Assert.Equal(4, AnalyticsScope.Single(4).RootEquipmentId);
    }
}
