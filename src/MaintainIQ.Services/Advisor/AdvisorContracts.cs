using MaintainIQ.Data.ReadModels;

namespace MaintainIQ.Services.Advisor;

public enum AdvisorMode
{
    Rules = 1,
    Llm = 2,
}

public enum RecommendationSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3,
}

/// <summary>
/// Advisor önerisi. Advisor yalnızca önerir; iş emri veya plan değişikliğini kullanıcı onaylayarak yapar.
/// <see cref="Evidence"/> her zaman doludur: önerinin "neden"i (kaç arıza, hangi tarihler, hangi iş emirleri).
/// </summary>
public sealed record Recommendation(
    string RuleCode,
    RecommendationSeverity Severity,
    string Title,
    string Description,
    IReadOnlyList<string> Evidence,
    string SuggestedAction,
    int? EquipmentId = null,
    int? SparePartId = null,
    int? MaintenancePlanId = null);

public interface IMaintenanceAdvisor
{
    AdvisorMode Mode { get; }

    Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        AnalyticsScope scope,
        CancellationToken cancellationToken = default);
}

/// <summary>Advisor eşikleri. UI katmanında appsettings.json "Advisor" bölümünden doldurulur.</summary>
public sealed class AdvisorOptions
{
    public AdvisorMode Mode { get; init; } = AdvisorMode.Rules;

    /// <summary>R1: pencere içinde bu sayıda veya daha fazla arıza → bakım aralığını kısalt.</summary>
    public int RepeatedFailureThreshold { get; init; } = 3;
    public int RepeatedFailureWindowDays { get; init; } = 183;

    /// <summary>R2: aynı ekipmanda aynı neden kodu bu sayıda tekrarlanırsa → kök neden incelemesi.</summary>
    public int SameCauseThreshold { get; init; } = 3;
    public int SameCauseWindowDays { get; init; } = 183;

    /// <summary>R3: bir parça pencere içinde bu kadar farklı iş emrinde değiştiyse → minimum stoğu artır.</summary>
    public int PartReplacementThreshold { get; init; } = 3;
    public int PartReplacementWindowDays { get; init; } = 90;

    /// <summary>R4: ekipmanın ortalama onarım süresi bu eşiği aşarsa → süreç incelemesi.</summary>
    public decimal MttrThresholdHours { get; init; } = 8m;
    public int MttrWindowDays { get; init; } = 183;
    public int MttrMinimumRepairs { get; init; } = 2;

    /// <summary>R5: vade + tolerans geçmişse plan gecikmiş sayılır.</summary>
    public int OverdueToleranceDays { get; init; }

    /// <summary>R6: garanti bitişine bu kadar gün veya daha az kaldıysa uyar.</summary>
    public int WarrantyWarningDays { get; init; } = 30;

    /// <summary>LLM API anahtarının okunacağı ortam değişkeni. Anahtar repoya girmez.</summary>
    public string LlmApiKeyEnvironmentVariable { get; init; } = "MAINTAINIQ_LLM_API_KEY";

    public int LookbackDays => new[]
    {
        RepeatedFailureWindowDays,
        SameCauseWindowDays,
        PartReplacementWindowDays,
        MttrWindowDays,
    }.Max();
}

/// <summary>Opsiyonel LLM genişlemesi için sınır. Çekirdek akış bu arayüze bağımlı değildir.</summary>
public interface ILlmClient
{
    Task<string?> SummarizeAsync(string prompt, CancellationToken cancellationToken = default);
}
