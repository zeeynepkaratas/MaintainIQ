using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Security;

namespace MaintainIQ.Services.Analytics;

public sealed record EquipmentCostItem(
    int EquipmentId,
    string Code,
    string Name,
    int ClosedWorkOrders,
    decimal PartsCost,
    decimal LaborCost,
    decimal TotalCost);

public sealed record KpiDashboard(
    AnalyticsScope Scope,
    KpiReport Summary,
    IReadOnlyList<FailureParetoItemDto> FailurePareto,
    IReadOnlyList<MonthlyFailureTrendDto> MonthlyFailureTrend,
    IReadOnlyList<PartConsumptionDto> TopPartConsumption,
    IReadOnlyList<EquipmentCostItem> EquipmentCosts,
    IReadOnlyList<TechnicianWorkloadDto> TechnicianWorkload,
    int CriticalStockCount);

public interface IKpiService
{
    Task<KpiReport> GetSummaryAsync(AnalyticsScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<KpiDashboard> GetDashboardAsync(AnalyticsScope scope, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}

public sealed class KpiService(
    IReportQueries reports,
    IPermissionService permissions,
    IClock clock,
    KpiOptions options) : IKpiService
{
    private const int TopPartCount = 10;

    public async Task<KpiReport> GetSummaryAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ViewAnalytics);
        ValidatePeriod(from, to);
        var facts = await reports.GetKpiFactsAsync(scope, from, to, cancellationToken).ConfigureAwait(false);
        return KpiCalculator.Calculate(facts, from, to, clock.UtcNow, options);
    }

    public async Task<KpiDashboard> GetDashboardAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(scope, from, to, cancellationToken).ConfigureAwait(false);
        var pareto = await reports.GetFailureParetoAsync(scope, from, to, cancellationToken).ConfigureAwait(false);
        var trend = await reports.GetMonthlyFailureTrendAsync(scope, from, to, cancellationToken).ConfigureAwait(false);
        var parts = await reports.GetTopPartConsumptionAsync(scope, from, to, TopPartCount, cancellationToken).ConfigureAwait(false);
        var costs = await reports.GetEquipmentCostsAsync(scope, from, to, cancellationToken).ConfigureAwait(false);
        var workload = await reports.GetTechnicianWorkloadAsync(from, to, cancellationToken).ConfigureAwait(false);
        var criticalStock = await reports.GetCriticalStockCountAsync(cancellationToken).ConfigureAwait(false);

        var costItems = costs
            .Select(cost =>
            {
                var labor = KpiCalculator.WorkOrderCost(0m, cost.LaborMinutes, options.HourlyLaborRate);
                return new EquipmentCostItem(
                    cost.EquipmentId, cost.Code, cost.Name, cost.ClosedWorkOrders,
                    cost.PartsCost, labor, cost.PartsCost + labor);
            })
            .OrderByDescending(item => item.TotalCost)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();

        return new KpiDashboard(scope, summary, pareto, trend, parts, costItems, workload, criticalStock);
    }

    private static void ValidatePeriod(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            throw new Domain.Common.DomainValidationException("Dönem bitişi başlangıçtan sonra olmalıdır.");
        }
    }
}
