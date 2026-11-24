using System.Text;
using MaintainIQ.Data.ReadModels;

namespace MaintainIQ.Services.Advisor;

/// <summary>
/// İsteğe bağlı LLM genişlemesi (stub düzeyi). Önerilerin kendisi her zaman kural motorundan gelir;
/// LLM yalnızca veriden bağımsız bir özet ekler. LLM erişilemezse sessizce kural sonuçlarına düşer.
/// </summary>
public sealed class LlmAdvisor(IMaintenanceAdvisor rules, ILlmClient client) : IMaintenanceAdvisor
{
    public AdvisorMode Mode => AdvisorMode.Llm;

    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        AnalyticsScope scope,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await rules.GetRecommendationsAsync(scope, cancellationToken).ConfigureAwait(false);
        if (recommendations.Count == 0)
        {
            return recommendations;
        }

        string? summary;
        try
        {
            summary = await client.SummarizeAsync(BuildPrompt(recommendations), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // İnternet yok, kota bitti vb.: çekirdek akış LLM'den bağımsızdır.
            return recommendations;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return recommendations;
        }

        return
        [
            .. recommendations,
            new Recommendation(
                "LLM-SUMMARY",
                RecommendationSeverity.Info,
                "Öneri özeti (LLM)",
                summary.Trim(),
                recommendations.Select(item => $"{item.RuleCode}: {item.Title}").ToArray(),
                "Özet bilgilendirme amaçlıdır; kararları kural önerilerinin kanıtlarına göre verin."),
        ];
    }

    internal static string BuildPrompt(IReadOnlyList<Recommendation> recommendations)
    {
        var builder = new StringBuilder()
            .AppendLine("Aşağıdaki bakım önerilerini bakım şefi için 3 cümlede Türkçe özetle. Yeni öneri uydurma.");
        foreach (var item in recommendations)
        {
            builder.Append("- [").Append(item.RuleCode).Append("] ").Append(item.Title).Append(": ").AppendLine(item.Description);
        }

        return builder.ToString();
    }
}

public sealed record AdvisorSelection(IMaintenanceAdvisor Advisor, AdvisorMode RequestedMode, string? FallbackReason)
{
    public AdvisorMode EffectiveMode => Advisor.Mode;
}

/// <summary>Advisor:Mode = Rules | Llm ayarına göre advisor seçer; LLM koşulları sağlanmazsa Rules'a düşer.</summary>
public static class AdvisorFactory
{
    public static AdvisorSelection Create(
        AdvisorOptions options,
        IMaintenanceAdvisor rules,
        Func<string, ILlmClient>? llmClientFactory = null,
        Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rules);
        if (options.Mode != AdvisorMode.Llm)
        {
            return new AdvisorSelection(rules, options.Mode, null);
        }

        var readEnvironment = environment ?? Environment.GetEnvironmentVariable;
        var apiKey = readEnvironment(options.LlmApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AdvisorSelection(rules, options.Mode,
                $"LLM API anahtarı bulunamadı ({options.LlmApiKeyEnvironmentVariable}); kural modu kullanılıyor.");
        }

        if (llmClientFactory is null)
        {
            return new AdvisorSelection(rules, options.Mode, "LLM istemcisi yapılandırılmadı; kural modu kullanılıyor.");
        }

        return new AdvisorSelection(new LlmAdvisor(rules, llmClientFactory(apiKey)), options.Mode, null);
    }
}
