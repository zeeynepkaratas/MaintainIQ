using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Advisor;
using NSubstitute;

namespace MaintainIQ.Tests.Services;

public sealed class AdvisorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly AdvisorOptions Defaults = new();

    // R1 ------------------------------------------------------------------
    [Fact]
    public void R1_ThreeFailuresInSixMonths_RecommendsShorterInterval()
    {
        var facts = Facts(failures: [Failure(1, 1, -10), Failure(2, 1, -40, "ELECTRICAL"), Failure(3, 1, -150, "HYDRAULIC")]);

        var item = Single(facts, "R1");

        Assert.Equal(1, item.EquipmentId);
        Assert.Equal(3, item.Evidence.Count);
        Assert.Contains("Arıza #3", item.Evidence[0], StringComparison.Ordinal);
    }

    [Fact]
    public void R1_FailureOutsideWindowOrBelowThreshold_DoesNotTrigger()
    {
        var facts = Facts(failures: [Failure(1, 1, -10), Failure(2, 1, -40, "ELECTRICAL"), Failure(3, 1, -200, "HYDRAULIC")]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R1");
    }

    [Fact]
    public void R1_ThresholdChange_ChangesBehaviour()
    {
        var facts = Facts(failures: [Failure(1, 1, -10, "ELECTRICAL"), Failure(2, 1, -40)]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R1");
        Assert.Contains(RuleBasedAdvisor.Evaluate(facts, Now, new AdvisorOptions { RepeatedFailureThreshold = 2 }),
            r => r.RuleCode == "R1");
    }

    // R2 ------------------------------------------------------------------
    [Fact]
    public void R2_SameCauseThreeTimes_RecommendsRootCauseAnalysis()
    {
        var facts = Facts(failures: [Failure(1, 1, -5), Failure(2, 1, -20), Failure(3, 1, -60)]);

        var item = Single(facts, "R2");

        Assert.Contains("MECHANICAL", item.Title, StringComparison.Ordinal);
        Assert.Equal(3, item.Evidence.Count);
    }

    [Fact]
    public void R2_SameCauseOnDifferentEquipment_DoesNotTrigger()
    {
        var facts = Facts(failures: [Failure(1, 1, -5), Failure(2, 2, -20), Failure(3, 1, -60)]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R2");
    }

    // R3 ------------------------------------------------------------------
    [Fact]
    public void R3_PartReplacedInThreeOrdersWithin90Days_RecommendsHigherMinimumStock()
    {
        var facts = Facts(parts: [Part(5, 100, -10), Part(5, 101, -30), Part(5, 102, -80), Part(5, 102, -80)]);

        var item = Single(facts, "R3");

        Assert.Equal(5, item.SparePartId);
        Assert.Equal(3, item.Evidence.Count);
        Assert.Contains("WO-102", item.Evidence[0], StringComparison.Ordinal);
    }

    [Fact]
    public void R3_SameOrderCountedOnceAndOldReplacementsIgnored()
    {
        var facts = Facts(parts: [Part(5, 100, -10), Part(5, 100, -10), Part(5, 101, -95)]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R3");
        Assert.Contains(RuleBasedAdvisor.Evaluate(facts, Now,
            new AdvisorOptions { PartReplacementThreshold = 2, PartReplacementWindowDays = 120 }), r => r.RuleCode == "R3");
    }

    // R4 ------------------------------------------------------------------
    [Fact]
    public void R4_AverageRepairAboveThreshold_RecommendsProcessReview()
    {
        var facts = Facts(repairs: [Repair(1, 1, -10, 10), Repair(2, 1, -20, 12)]);

        var item = Single(facts, "R4");

        Assert.Contains("11", item.Description, StringComparison.Ordinal);
        Assert.Equal(2, item.Evidence.Count);
    }

    [Fact]
    public void R4_BelowThresholdOrTooFewRepairs_DoesNotTrigger()
    {
        var fast = Facts(repairs: [Repair(1, 1, -10, 2), Repair(2, 1, -20, 3)]);
        var single = Facts(repairs: [Repair(1, 1, -10, 30)]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(fast, Now, Defaults), r => r.RuleCode == "R4");
        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(single, Now, Defaults), r => r.RuleCode == "R4");
        Assert.Contains(RuleBasedAdvisor.Evaluate(fast, Now, new AdvisorOptions { MttrThresholdHours = 2 }),
            r => r.RuleCode == "R4");
    }

    // R5 ------------------------------------------------------------------
    [Fact]
    public void R5_OverduePlan_IsGroupedPerPlanWithEvidence()
    {
        var facts = Facts(overdue:
        [
            Overdue(7, 2, -12, "WO-PM-1"),
            Overdue(7, 2, -3, null),
        ]);

        var item = Single(facts, "R5");

        Assert.Equal(7, item.MaintenancePlanId);
        Assert.Equal(RecommendationSeverity.Warning, item.Severity);
        Assert.Contains("12 gün", item.Description, StringComparison.Ordinal);
        Assert.Equal(2, item.Evidence.Count);
    }

    [Fact]
    public void R5_OnCriticalEquipment_IsCritical_AndToleranceSuppressesRecentDue()
    {
        var critical = Facts(overdue: [Overdue(8, 1, -5, null)]);
        var withinTolerance = Facts(overdue: [Overdue(8, 1, -2, null)]);

        Assert.Equal(RecommendationSeverity.Critical, Single(critical, "R5").Severity);
        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(withinTolerance, Now, new AdvisorOptions { OverdueToleranceDays = 3 }),
            r => r.RuleCode == "R5");
    }

    // R6 ------------------------------------------------------------------
    [Fact]
    public void R6_WarrantyEndingWithinWindow_Warns()
    {
        var facts = Facts(equipment: [Eq(1, critical: false, warrantyInDays: 20)]);

        var item = Single(facts, "R6");

        Assert.Contains("20 gün", item.Description, StringComparison.Ordinal);
        Assert.Single(item.Evidence);
    }

    [Fact]
    public void R6_ExpiredOrFarWarranty_DoesNotTrigger()
    {
        var facts = Facts(equipment: [Eq(1, false, warrantyInDays: -1), Eq(2, false, warrantyInDays: 90)]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R6");
        Assert.Contains(RuleBasedAdvisor.Evaluate(facts, Now, new AdvisorOptions { WarrantyWarningDays = 120 }),
            r => r.RuleCode == "R6");
    }

    // R7 ------------------------------------------------------------------
    [Fact]
    public void R7_OpenCriticalFailureOnCriticalEquipment_IsCritical()
    {
        var facts = Facts(failures: [Failure(9, 1, -1, severity: FailureSeverity.Critical, status: FailureStatus.Open)]);

        var item = Single(facts, "R7");

        Assert.Equal(RecommendationSeverity.Critical, item.Severity);
        Assert.Contains("Arıza #9", item.Evidence[0], StringComparison.Ordinal);
    }

    [Fact]
    public void R7_ResolvedFailureOrNonCriticalEquipment_DoesNotTrigger()
    {
        var facts = Facts(failures:
        [
            Failure(9, 1, -1, severity: FailureSeverity.Critical, status: FailureStatus.Resolved),
            Failure(10, 2, -1, severity: FailureSeverity.Critical, status: FailureStatus.Open),
            Failure(11, 1, -1, severity: FailureSeverity.High, status: FailureStatus.Open),
        ]);

        Assert.DoesNotContain(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == "R7");
    }

    // Ortak ---------------------------------------------------------------
    [Fact]
    public void Evaluate_EveryRecommendationHasEvidence_AndIsOrderedBySeverity()
    {
        var facts = Facts(
            failures: [Failure(1, 1, -5), Failure(2, 1, -20), Failure(3, 1, -60),
                Failure(9, 1, -1, severity: FailureSeverity.Critical, status: FailureStatus.InReview)],
            parts: [Part(5, 100, -10), Part(5, 101, -30), Part(5, 102, -80)],
            repairs: [Repair(1, 1, -10, 10), Repair(2, 1, -20, 12)],
            overdue: [Overdue(7, 2, -12, null)],
            equipment: [Eq(1, true, 10), Eq(2, false, null)]);

        var result = RuleBasedAdvisor.Evaluate(facts, Now, Defaults);

        Assert.Equal(new[] { "R1", "R2", "R3", "R4", "R5", "R6", "R7" }, result.Select(r => r.RuleCode).Order().ToArray());
        Assert.All(result, r => Assert.NotEmpty(r.Evidence));
        Assert.All(result, r => Assert.False(string.IsNullOrWhiteSpace(r.SuggestedAction)));
        Assert.Equal(RecommendationSeverity.Critical, result[0].Severity);
        Assert.Equal(RecommendationSeverity.Info, result[^1].Severity);
    }

    [Fact]
    public void Evaluate_UnknownEquipment_UsesIdLabel()
    {
        var facts = Facts(failures: [Failure(1, 99, -1), Failure(2, 99, -2), Failure(3, 99, -3)]);

        Assert.StartsWith("Ekipman #99", Single(facts, "R1").Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRecommendations_LoadsFactsForLongestWindow_AndRequiresAnalyticsPermission()
    {
        var fixture = new ServiceFixture();
        var reports = Substitute.For<IReportQueries>();
        reports.GetAdvisorFactsAsync(AnalyticsScope.Plant, ServiceFixture.Now, 183, Arg.Any<CancellationToken>())
            .Returns(Facts(failures: [Failure(1, 1, -1), Failure(2, 1, -2), Failure(3, 1, -3)]));
        var advisor = new RuleBasedAdvisor(reports, fixture.Permissions, fixture.Clock, Defaults);

        var result = await advisor.GetRecommendationsAsync(AnalyticsScope.Plant);

        Assert.Equal(AdvisorMode.Rules, advisor.Mode);
        Assert.Contains(result, r => r.RuleCode == "R1");
        fixture.CurrentUser.SignOut();
        await Assert.ThrowsAsync<PermissionDeniedException>(() => advisor.GetRecommendationsAsync(AnalyticsScope.Plant));
    }

    [Fact]
    public void Factory_RulesMode_ReturnsRuleAdvisor()
    {
        var rules = StubAdvisor([]);

        var selection = AdvisorFactory.Create(new AdvisorOptions(), rules);

        Assert.Same(rules, selection.Advisor);
        Assert.Equal(AdvisorMode.Rules, selection.EffectiveMode);
        Assert.Null(selection.FallbackReason);
    }

    [Fact]
    public void Factory_LlmModeWithoutKeyOrClient_FallsBackToRules()
    {
        var rules = StubAdvisor([]);
        var options = new AdvisorOptions { Mode = AdvisorMode.Llm };

        var noKey = AdvisorFactory.Create(options, rules, _ => Substitute.For<ILlmClient>(), _ => null);
        var noClient = AdvisorFactory.Create(options, rules, null, _ => "secret");

        Assert.Equal(AdvisorMode.Rules, noKey.EffectiveMode);
        Assert.Equal(AdvisorMode.Llm, noKey.RequestedMode);
        Assert.Contains("MAINTAINIQ_LLM_API_KEY", noKey.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(AdvisorMode.Rules, noClient.EffectiveMode);
        Assert.NotNull(noClient.FallbackReason);
    }

    [Fact]
    public async Task Factory_LlmModeWithKey_AddsSummaryOnTopOfRuleRecommendations()
    {
        var rules = StubAdvisor([Recommendation("R1")]);
        var client = Substitute.For<ILlmClient>();
        client.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("Pompa sık arızalanıyor.");
        string? receivedKey = null;

        var selection = AdvisorFactory.Create(
            new AdvisorOptions { Mode = AdvisorMode.Llm }, rules, key => { receivedKey = key; return client; }, _ => "secret");
        var result = await selection.Advisor.GetRecommendationsAsync(AnalyticsScope.Plant);

        Assert.Equal("secret", receivedKey);
        Assert.Equal(AdvisorMode.Llm, selection.EffectiveMode);
        Assert.Equal(new[] { "R1", "LLM-SUMMARY" }, result.Select(r => r.RuleCode).ToArray());
        Assert.Equal("Pompa sık arızalanıyor.", result[1].Description);
        await client.Received(1).SummarizeAsync(Arg.Is<string>(p => p.Contains("[R1]")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LlmAdvisor_WhenServiceUnavailable_ReturnsRuleRecommendationsUnchanged()
    {
        var recommendations = new[] { Recommendation("R5") };
        var client = Substitute.For<ILlmClient>();
        client.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string?>(new HttpRequestException("offline")));
        var advisor = new LlmAdvisor(StubAdvisor(recommendations), client);

        var result = await advisor.GetRecommendationsAsync(AnalyticsScope.Plant);

        Assert.Equal(recommendations, result);
    }

    [Fact]
    public async Task LlmAdvisor_WithNoRecommendationsOrBlankSummary_DoesNotAddSummary()
    {
        var client = Substitute.For<ILlmClient>();
        client.SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("  ");

        var empty = await new LlmAdvisor(StubAdvisor([]), client).GetRecommendationsAsync(AnalyticsScope.Plant);
        var blank = await new LlmAdvisor(StubAdvisor([Recommendation("R2")]), client).GetRecommendationsAsync(AnalyticsScope.Plant);

        Assert.Empty(empty);
        Assert.Single(blank);
        await client.Received(1).SummarizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static Recommendation Single(AdvisorFactSet facts, string rule) =>
        Assert.Single(RuleBasedAdvisor.Evaluate(facts, Now, Defaults), r => r.RuleCode == rule);

    private static IMaintenanceAdvisor StubAdvisor(IReadOnlyList<Recommendation> recommendations)
    {
        var advisor = Substitute.For<IMaintenanceAdvisor>();
        advisor.Mode.Returns(AdvisorMode.Rules);
        advisor.GetRecommendationsAsync(Arg.Any<AnalyticsScope>(), Arg.Any<CancellationToken>()).Returns(recommendations);
        return advisor;
    }

    private static Recommendation Recommendation(string rule) =>
        new(rule, RecommendationSeverity.Warning, $"Başlık {rule}", "Açıklama", ["kanıt"], "Eylem");

    private static AdvisorFactSet Facts(
        IReadOnlyList<FailureFactDto>? failures = null,
        IReadOnlyList<PartReplacementFactDto>? parts = null,
        IReadOnlyList<WorkOrderFactDto>? repairs = null,
        IReadOnlyList<OverduePlanFactDto>? overdue = null,
        IReadOnlyList<ScopeEquipmentDto>? equipment = null) => new()
    {
        Equipment = equipment ?? [Eq(1, critical: true, warrantyInDays: null), Eq(2, critical: false, warrantyInDays: null)],
        Failures = failures ?? [],
        PartReplacements = parts ?? [],
        CompletedCorrectiveWorkOrders = repairs ?? [],
        OverduePlans = overdue ?? [],
    };

    private static ScopeEquipmentDto Eq(int id, bool critical, int? warrantyInDays) => new()
    {
        Id = id,
        Code = $"EQ-{id}",
        Name = $"Pompa {id}",
        IsCritical = critical,
        WarrantyEndDate = warrantyInDays is int days ? Now.UtcDateTime.Date.AddDays(days) : null,
    };

    private static FailureFactDto Failure(
        int id, int equipmentId, int daysAgo, string cause = "MECHANICAL",
        FailureSeverity severity = FailureSeverity.Medium, FailureStatus status = FailureStatus.Resolved) => new()
    {
        Id = id,
        EquipmentId = equipmentId,
        CauseCode = cause,
        CauseName = cause == "MECHANICAL" ? "Mekanik arıza" : cause,
        Severity = severity,
        Status = status,
        ReportedAt = Now.AddDays(daysAgo),
    };

    private static PartReplacementFactDto Part(int partId, int workOrderId, int daysAgo) => new()
    {
        SparePartId = partId,
        Code = $"SP-{partId}",
        Name = "Rulman",
        StockQuantity = 2,
        MinimumStock = 1,
        WorkOrderId = workOrderId,
        WorkOrderNumber = $"WO-{workOrderId}",
        CompletedAt = Now.AddDays(daysAgo),
    };

    private static WorkOrderFactDto Repair(int id, int equipmentId, int daysAgo, int hours) => new()
    {
        Id = id,
        Number = $"WO-{id}",
        EquipmentId = equipmentId,
        Status = WorkOrderStatus.Completed,
        CreatedAt = Now.AddDays(daysAgo - 1),
        ActualStartAt = Now.AddDays(daysAgo).AddHours(-hours),
        CompletedAt = Now.AddDays(daysAgo),
    };

    private static OverduePlanFactDto Overdue(int planId, int equipmentId, int daysAgo, string? number) => new()
    {
        MaintenancePlanId = planId,
        EquipmentId = equipmentId,
        PlanName = "Aylık yağlama",
        DueDate = Now.UtcDateTime.Date.AddDays(daysAgo),
        OpenWorkOrderNumber = number,
    };
}
