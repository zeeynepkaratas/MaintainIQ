using System.Globalization;
using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Advisor;

/// <summary>
/// Kural tabanlı öngörücü bakım önerileri (R1-R7). <see cref="Evaluate"/> saf fonksiyondur;
/// veri <see cref="IReportQueries.GetAdvisorFactsAsync"/> ile tek seferde okunur.
/// </summary>
public sealed class RuleBasedAdvisor(
    IReportQueries reports,
    IPermissionService permissions,
    IClock clock,
    AdvisorOptions options) : IMaintenanceAdvisor
{
    private static readonly CultureInfo Turkish = ResolveCulture();

    public AdvisorMode Mode => AdvisorMode.Rules;

    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        AnalyticsScope scope,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ViewAnalytics);
        var now = clock.UtcNow;
        var facts = await reports.GetAdvisorFactsAsync(scope, now, options.LookbackDays, cancellationToken).ConfigureAwait(false);
        return Evaluate(facts, now, options);
    }

    public static IReadOnlyList<Recommendation> Evaluate(AdvisorFactSet facts, DateTimeOffset now, AdvisorOptions options)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(options);
        var equipment = facts.Equipment.ToDictionary(item => item.Id);
        var recommendations = new List<Recommendation>();
        recommendations.AddRange(RepeatedFailures(facts, equipment, now, options));
        recommendations.AddRange(RepeatedCause(facts, equipment, now, options));
        recommendations.AddRange(FrequentPartReplacement(facts, now, options));
        recommendations.AddRange(HighMttr(facts, equipment, now, options));
        recommendations.AddRange(OverduePlans(facts, equipment, now, options));
        recommendations.AddRange(WarrantyEnding(facts, now, options));
        recommendations.AddRange(CriticalEquipmentWithCriticalFailure(facts, equipment));
        return recommendations
            .OrderByDescending(item => item.Severity)
            .ThenBy(item => item.RuleCode, StringComparer.Ordinal)
            .ThenBy(item => item.EquipmentId)
            .ToArray();
    }

    // R1: son N günde >= eşik arıza → bakım aralığını kısalt.
    private static IEnumerable<Recommendation> RepeatedFailures(
        AdvisorFactSet facts, IReadOnlyDictionary<int, ScopeEquipmentDto> equipment, DateTimeOffset now, AdvisorOptions options)
    {
        var since = now.AddDays(-options.RepeatedFailureWindowDays);
        return facts.Failures
            .Where(f => f.ReportedAt >= since && f.ReportedAt <= now)
            .GroupBy(f => f.EquipmentId)
            .Where(group => group.Count() >= options.RepeatedFailureThreshold)
            .Select(group => new Recommendation(
                "R1",
                RecommendationSeverity.Warning,
                $"{Label(equipment, group.Key)}: sık arıza",
                $"Son {options.RepeatedFailureWindowDays} günde {group.Count()} arıza bildirildi (eşik {options.RepeatedFailureThreshold}).",
                group.OrderBy(f => f.ReportedAt).Select(FailureEvidence).ToArray(),
                "Önleyici bakım aralığını kısaltın veya ekipmana yeni bir bakım planı ekleyin.",
                group.Key));
    }

    // R2: aynı ekipmanda aynı neden kodu >= eşik → kök neden incelemesi.
    private static IEnumerable<Recommendation> RepeatedCause(
        AdvisorFactSet facts, IReadOnlyDictionary<int, ScopeEquipmentDto> equipment, DateTimeOffset now, AdvisorOptions options)
    {
        var since = now.AddDays(-options.SameCauseWindowDays);
        return facts.Failures
            .Where(f => f.ReportedAt >= since && f.ReportedAt <= now)
            .GroupBy(f => (f.EquipmentId, f.CauseCode))
            .Where(group => group.Count() >= options.SameCauseThreshold)
            .Select(group => new Recommendation(
                "R2",
                RecommendationSeverity.Warning,
                $"{Label(equipment, group.Key.EquipmentId)}: tekrarlayan neden {group.Key.CauseCode}",
                $"'{group.First().CauseName}' nedeni son {options.SameCauseWindowDays} günde {group.Count()} kez görüldü (eşik {options.SameCauseThreshold}).",
                group.OrderBy(f => f.ReportedAt).Select(FailureEvidence).ToArray(),
                "Kök neden analizi (5 Neden / balık kılçığı) başlatın; kalıcı düzeltici faaliyet tanımlayın.",
                group.Key.EquipmentId));
    }

    // R3: bir parça son N günde >= eşik farklı iş emrinde değişti → minimum stoğu artır.
    private static IEnumerable<Recommendation> FrequentPartReplacement(
        AdvisorFactSet facts, DateTimeOffset now, AdvisorOptions options)
    {
        var since = now.AddDays(-options.PartReplacementWindowDays);
        return facts.PartReplacements
            .Where(p => p.CompletedAt >= since && p.CompletedAt <= now)
            .GroupBy(p => p.SparePartId)
            .Select(group => (Part: group.First(), Orders: group.DistinctBy(p => p.WorkOrderId).OrderBy(p => p.CompletedAt).ToArray()))
            .Where(item => item.Orders.Length >= options.PartReplacementThreshold)
            .Select(item => new Recommendation(
                "R3",
                RecommendationSeverity.Info,
                $"{item.Part.Code} {item.Part.Name}: sık değişim",
                $"Parça son {options.PartReplacementWindowDays} günde {item.Orders.Length} iş emrinde kullanıldı (eşik {options.PartReplacementThreshold}). Stok {item.Part.StockQuantity.ToString("0.###", Turkish)}, minimum {item.Part.MinimumStock.ToString("0.###", Turkish)}.",
                item.Orders.Select(p => $"{p.WorkOrderNumber} - {Date(p.CompletedAt)}").ToArray(),
                "Minimum stok seviyesini artırın ve parçanın neden sık değiştiğini inceleyin.",
                SparePartId: item.Part.SparePartId));
    }

    // R4: ortalama onarım süresi eşik üstü → süreç incelemesi.
    private static IEnumerable<Recommendation> HighMttr(
        AdvisorFactSet facts, IReadOnlyDictionary<int, ScopeEquipmentDto> equipment, DateTimeOffset now, AdvisorOptions options)
    {
        var since = now.AddDays(-options.MttrWindowDays);
        foreach (var group in facts.CompletedCorrectiveWorkOrders
            .Where(w => w.ActualStartAt is not null && w.CompletedAt is { } done && done >= since && done <= now)
            .GroupBy(w => w.EquipmentId))
        {
            var repairs = group
                .Select(w => (Order: w, Hours: (decimal)(w.CompletedAt!.Value - w.ActualStartAt!.Value).TotalHours))
                .OrderBy(item => item.Order.CompletedAt)
                .ToArray();
            if (repairs.Length < options.MttrMinimumRepairs)
            {
                continue;
            }

            var mttr = Math.Round(repairs.Average(item => item.Hours), 2, MidpointRounding.AwayFromZero);
            if (mttr <= options.MttrThresholdHours)
            {
                continue;
            }

            yield return new Recommendation(
                "R4",
                RecommendationSeverity.Warning,
                $"{Label(equipment, group.Key)}: yüksek MTTR",
                $"Son {options.MttrWindowDays} günde {repairs.Length} onarımın ortalaması {mttr.ToString("0.##", Turkish)} saat (eşik {options.MttrThresholdHours.ToString("0.##", Turkish)} saat).",
                repairs.Select(item => $"{item.Order.Number} - {item.Hours.ToString("0.##", Turkish)} saat ({Date(item.Order.CompletedAt!.Value)})").ToArray(),
                "Onarım sürecini inceleyin: parça hazırlığı, teknisyen yetkinliği ve iş talimatlarını gözden geçirin.",
                group.Key);
        }
    }

    // R5: gecikmiş bakım planı (üretilmemiş vade veya açık önleyici iş emri).
    private static IEnumerable<Recommendation> OverduePlans(
        AdvisorFactSet facts, IReadOnlyDictionary<int, ScopeEquipmentDto> equipment, DateTimeOffset now, AdvisorOptions options)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        return facts.OverduePlans
            .Where(p => DateOnly.FromDateTime(p.DueDate).AddDays(options.OverdueToleranceDays) < today)
            .GroupBy(p => p.MaintenancePlanId)
            .Select(group =>
            {
                var oldest = group.MinBy(p => p.DueDate)!;
                var daysLate = today.DayNumber - DateOnly.FromDateTime(oldest.DueDate).DayNumber;
                var critical = equipment.TryGetValue(oldest.EquipmentId, out var eq) && eq.IsCritical;
                return new Recommendation(
                    "R5",
                    critical ? RecommendationSeverity.Critical : RecommendationSeverity.Warning,
                    $"{oldest.PlanName}: gecikmiş bakım",
                    $"{Label(equipment, oldest.EquipmentId)} için planlı bakım {daysLate} gün gecikti.",
                    group.OrderBy(p => p.DueDate)
                        .Select(p => p.OpenWorkOrderNumber is null
                            ? $"Vade {Date(p.DueDate)} - iş emri henüz üretilmedi"
                            : $"Vade {Date(p.DueDate)} - açık iş emri {p.OpenWorkOrderNumber}")
                        .ToArray(),
                    "Bakım iş emrini önceliklendirip atayın veya plan motorunu çalıştırın.",
                    oldest.EquipmentId,
                    MaintenancePlanId: oldest.MaintenancePlanId);
            });
    }

    // R6: garanti bitimi yaklaşan ekipman.
    private static IEnumerable<Recommendation> WarrantyEnding(AdvisorFactSet facts, DateTimeOffset now, AdvisorOptions options)
    {
        var today = DateOnly.FromDateTime(now.DateTime);
        return facts.Equipment
            .Where(e => e.WarrantyEndDate is not null)
            .Select(e => (Equipment: e, DaysLeft: DateOnly.FromDateTime(e.WarrantyEndDate!.Value).DayNumber - today.DayNumber))
            .Where(item => item.DaysLeft >= 0 && item.DaysLeft <= options.WarrantyWarningDays)
            .Select(item => new Recommendation(
                "R6",
                RecommendationSeverity.Info,
                $"{item.Equipment.Code} {item.Equipment.Name}: garanti bitiyor",
                $"Garanti {item.DaysLeft} gün içinde sona eriyor (uyarı eşiği {options.WarrantyWarningDays} gün).",
                [$"Garanti bitişi {Date(item.Equipment.WarrantyEndDate!.Value)}"],
                "Garanti bitmeden kapsamlı kontrol yaptırın ve bilinen sorunları üreticiye bildirin.",
                item.Equipment.Id));
    }

    // R7: kritik ekipmanda bekleyen kritik arıza.
    private static IEnumerable<Recommendation> CriticalEquipmentWithCriticalFailure(
        AdvisorFactSet facts, IReadOnlyDictionary<int, ScopeEquipmentDto> equipment)
    {
        return facts.Failures
            .Where(f => f.Severity == FailureSeverity.Critical
                && f.Status is FailureStatus.Open or FailureStatus.InReview
                && equipment.TryGetValue(f.EquipmentId, out var eq) && eq.IsCritical)
            .GroupBy(f => f.EquipmentId)
            .Select(group => new Recommendation(
                "R7",
                RecommendationSeverity.Critical,
                $"{Label(equipment, group.Key)}: kritik ekipmanda bekleyen kritik arıza",
                $"Kritik ekipmanda {group.Count()} adet çözülmemiş kritik arıza var.",
                group.OrderBy(f => f.ReportedAt).Select(FailureEvidence).ToArray(),
                "Arızayı hemen düzeltici iş emrine dönüştürüp en yüksek öncelikle atayın.",
                group.Key));
    }

    private static string FailureEvidence(FailureFactDto failure) =>
        $"Arıza #{failure.Id} - {Date(failure.ReportedAt)} - {failure.CauseCode} ({failure.Severity})";

    private static string Label(IReadOnlyDictionary<int, ScopeEquipmentDto> equipment, int id) =>
        equipment.TryGetValue(id, out var item) ? $"{item.Code} {item.Name}" : $"Ekipman #{id}";

    private static string Date(DateTimeOffset value) => value.ToString("dd.MM.yyyy", Turkish);

    private static string Date(DateTime value) => value.ToString("dd.MM.yyyy", Turkish);

    private static CultureInfo ResolveCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("tr-TR");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
