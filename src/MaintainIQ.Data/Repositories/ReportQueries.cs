using Dapper;
using MaintainIQ.Data.Connection;
using MaintainIQ.Data.ReadModels;

namespace MaintainIQ.Data.Repositories;

/// <summary>
/// Salt okunur analiz sorguları. Kapsam (tüm tesis / alt ağaç / tek ekipman) her sorguda özyinelemeli
/// CTE ile <c>@Scope</c> tablo değişkenine alınır; sıralama ve kümülatif değerler pencere fonksiyonlarıyla
/// SQL tarafında hesaplanır. Transaction açmaz; her çağrı kendi bağlantısını kullanır.
/// </summary>
public sealed class ReportQueries(IDbConnectionFactory connectionFactory) : IReportQueries
{
    private const string ScopeSql = """
        DECLARE @Scope TABLE (Id int NOT NULL PRIMARY KEY);
        WITH Tree AS
        (
            SELECT Id FROM dbo.Equipment
            WHERE IsDeleted = 0 AND (@RootId IS NULL OR Id = @RootId)
            UNION ALL
            SELECT e.Id FROM dbo.Equipment e
            INNER JOIN Tree t ON e.ParentId = t.Id
            WHERE e.IsDeleted = 0 AND @RootId IS NOT NULL AND @IncludeDescendants = 1
        )
        INSERT @Scope (Id) SELECT DISTINCT Id FROM Tree OPTION (MAXRECURSION 100);

        """;

    private const string ScopeEquipmentSql = """
        SELECT e.Id, e.Code, e.Name, e.Status, e.IsCritical, e.WarrantyEndDate
        FROM dbo.Equipment e INNER JOIN @Scope s ON s.Id = e.Id
        ORDER BY e.Code;

        """;

    private const string PartsCostApply = """
        OUTER APPLY
        (
            SELECT SUM(wp.Quantity * wp.UnitCostSnapshot) AS PartsCost
            FROM dbo.WorkOrderPart wp WHERE wp.WorkOrderId = w.Id
        ) pc
        """;

    public async Task<KpiFactSet> GetKpiFactsAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        const string facts = $"""
            SELECT f.Id, f.EquipmentId, c.Code AS CauseCode, c.Name AS CauseName, f.Severity, f.Status,
                   f.ReportedAt, f.ResolvedAt, w.CompletedAt AS WorkOrderCompletedAt
            FROM dbo.Failure f
            INNER JOIN @Scope s ON s.Id = f.EquipmentId
            INNER JOIN dbo.FailureCause c ON c.Id = f.CauseId
            LEFT JOIN dbo.WorkOrder w ON w.FailureId = f.Id AND w.Status <> 7
            WHERE f.Status <> 4
              AND f.ReportedAt < @To
              AND (f.ReportedAt >= @From
                   OR COALESCE(w.CompletedAt, f.ResolvedAt) IS NULL
                   OR COALESCE(w.CompletedAt, f.ResolvedAt) >= @From)
            ORDER BY f.ReportedAt;

            SELECT w.Id, w.Number, w.EquipmentId,
                   CAST(CASE WHEN w.WorkOrderType = 2 THEN 1 ELSE 0 END AS bit) AS IsPreventive,
                   w.FailureId, w.MaintenancePlanId, w.DueDate, w.Status, w.AssignedTechnicianId,
                   w.CreatedAt, w.ActualStartAt, w.CompletedAt, w.ClosedAt, w.LaborMinutes,
                   COALESCE(pc.PartsCost, 0) AS PartsCost
            FROM dbo.WorkOrder w
            INNER JOIN @Scope s ON s.Id = w.EquipmentId
            {PartsCostApply}
            WHERE w.CreatedAt < @To
              AND (w.Status IN (1, 2, 3, 4)
                   OR w.CreatedAt >= @From
                   OR w.CompletedAt >= @From
                   OR w.ClosedAt >= @From
                   OR w.DueDate >= @FromDate)
            ORDER BY w.CreatedAt;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            ScopeSql + ScopeEquipmentSql + facts,
            Parameters(scope, from, to),
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new KpiFactSet
        {
            Equipment = (await grid.ReadAsync<ScopeEquipmentDto>().ConfigureAwait(false)).ToArray(),
            Failures = (await grid.ReadAsync<FailureFactDto>().ConfigureAwait(false)).ToArray(),
            WorkOrders = (await grid.ReadAsync<WorkOrderFactDto>().ConfigureAwait(false)).ToArray(),
        };
    }

    public Task<IReadOnlyList<FailureParetoItemDto>> GetFailureParetoAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH Counts AS
            (
                SELECT c.Code AS CauseCode, c.Name AS CauseName, COUNT(*) AS FailureCount
                FROM dbo.Failure f
                INNER JOIN @Scope s ON s.Id = f.EquipmentId
                INNER JOIN dbo.FailureCause c ON c.Id = f.CauseId
                WHERE f.Status <> 4 AND f.ReportedAt >= @From AND f.ReportedAt < @To
                GROUP BY c.Code, c.Name
            )
            SELECT CauseCode, CauseName, FailureCount,
                   CAST(100.0 * FailureCount / SUM(FailureCount) OVER () AS decimal(9,2)) AS SharePercent,
                   CAST(100.0 * SUM(FailureCount) OVER (ORDER BY FailureCount DESC, CauseCode ROWS UNBOUNDED PRECEDING)
                        / SUM(FailureCount) OVER () AS decimal(9,2)) AS CumulativePercent
            FROM Counts
            ORDER BY FailureCount DESC, CauseCode;
            """;
        return QueryScopedAsync<FailureParetoItemDto>(sql, Parameters(scope, from, to), cancellationToken);
    }

    public Task<IReadOnlyList<MonthlyFailureTrendDto>> GetMonthlyFailureTrendAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH Months AS
            (
                SELECT DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1) AS MonthStart
                UNION ALL
                SELECT DATEADD(month, 1, MonthStart) FROM Months WHERE DATEADD(month, 1, MonthStart) < @ToDate
            ),
            Counts AS
            (
                SELECT DATEFROMPARTS(YEAR(f.ReportedAt), MONTH(f.ReportedAt), 1) AS MonthStart, COUNT(*) AS FailureCount
                FROM dbo.Failure f
                INNER JOIN @Scope s ON s.Id = f.EquipmentId
                WHERE f.Status <> 4 AND f.ReportedAt >= @From AND f.ReportedAt < @To
                GROUP BY DATEFROMPARTS(YEAR(f.ReportedAt), MONTH(f.ReportedAt), 1)
            )
            SELECT YEAR(m.MonthStart) AS [Year], MONTH(m.MonthStart) AS [Month],
                   COALESCE(c.FailureCount, 0) AS FailureCount,
                   CAST(AVG(CAST(COALESCE(c.FailureCount, 0) AS decimal(9,2)))
                        OVER (ORDER BY m.MonthStart ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS decimal(9,2)) AS MovingAverage3
            FROM Months m
            LEFT JOIN Counts c ON c.MonthStart = m.MonthStart
            ORDER BY m.MonthStart
            OPTION (MAXRECURSION 600);
            """;
        return QueryScopedAsync<MonthlyFailureTrendDto>(sql, Parameters(scope, from, to), cancellationToken);
    }

    public Task<IReadOnlyList<PartConsumptionDto>> GetTopPartConsumptionAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        int top,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH Usage AS
            (
                SELECT wp.SparePartId,
                       SUM(wp.Quantity) AS TotalQuantity,
                       SUM(wp.Quantity * wp.UnitCostSnapshot) AS TotalCost,
                       COUNT(DISTINCT wp.WorkOrderId) AS WorkOrderCount
                FROM dbo.WorkOrderPart wp
                INNER JOIN dbo.WorkOrder w ON w.Id = wp.WorkOrderId
                INNER JOIN @Scope s ON s.Id = w.EquipmentId
                WHERE w.CompletedAt >= @From AND w.CompletedAt < @To
                GROUP BY wp.SparePartId
            )
            SELECT TOP (@Top)
                   CAST(RANK() OVER (ORDER BY u.TotalQuantity DESC) AS int) AS [Rank],
                   u.SparePartId, p.Code, p.Name, u.TotalQuantity, u.TotalCost, u.WorkOrderCount
            FROM Usage u
            INNER JOIN dbo.SparePart p ON p.Id = u.SparePartId
            ORDER BY [Rank], p.Code;
            """;
        var parameters = Parameters(scope, from, to);
        parameters.Add("Top", Math.Clamp(top, 1, 100));
        return QueryScopedAsync<PartConsumptionDto>(sql, parameters, cancellationToken);
    }

    public Task<IReadOnlyList<EquipmentCostDto>> GetEquipmentCostsAsync(
        AnalyticsScope scope,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT e.Id AS EquipmentId, e.Code, e.Name,
                   COALESCE(SUM(pc.PartsCost), 0) AS PartsCost,
                   COALESCE(SUM(w.LaborMinutes), 0) AS LaborMinutes,
                   COUNT(w.Id) AS ClosedWorkOrders
            FROM @Scope s
            INNER JOIN dbo.Equipment e ON e.Id = s.Id
            LEFT JOIN dbo.WorkOrder w
                ON w.EquipmentId = e.Id AND w.Status = 6 AND w.ClosedAt >= @From AND w.ClosedAt < @To
            {PartsCostApply}
            GROUP BY e.Id, e.Code, e.Name
            ORDER BY COALESCE(SUM(pc.PartsCost), 0) DESC, e.Code;
            """;
        return QueryScopedAsync<EquipmentCostDto>(sql, Parameters(scope, from, to), cancellationToken);
    }

    public async Task<IReadOnlyList<TechnicianWorkloadDto>> GetTechnicianWorkloadAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT p.Id AS TechnicianId, CONCAT(p.FirstName, N' ', p.LastName) AS FullName,
                   SUM(CASE WHEN w.Status IN (2, 3, 4) THEN 1 ELSE 0 END) AS OpenWorkOrders,
                   SUM(CASE WHEN w.CompletedAt >= @From AND w.CompletedAt < @To THEN 1 ELSE 0 END) AS CompletedInPeriod,
                   CAST(SUM(CASE WHEN w.CompletedAt >= @From AND w.CompletedAt < @To THEN w.LaborMinutes ELSE 0 END) / 60.0
                        AS decimal(18,2)) AS LaborHoursInPeriod
            FROM dbo.Person p
            LEFT JOIN dbo.WorkOrder w ON w.AssignedTechnicianId = p.Id
            WHERE p.PersonType = 3 AND p.IsActive = 1
            GROUP BY p.Id, p.FirstName, p.LastName
            ORDER BY OpenWorkOrders DESC, FullName;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<TechnicianWorkloadDto>(new CommandDefinition(
            sql, new { From = from, To = to }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToArray();
    }

    public async Task<int> GetCriticalStockCountAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.SparePart WHERE StockQuantity < MinimumStock;";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<AdvisorFactSet> GetAdvisorFactsAsync(
        AnalyticsScope scope,
        DateTimeOffset now,
        int lookbackDays,
        CancellationToken cancellationToken = default)
    {
        const string facts = """
            SELECT f.Id, f.EquipmentId, c.Code AS CauseCode, c.Name AS CauseName, f.Severity, f.Status,
                   f.ReportedAt, f.ResolvedAt, w.CompletedAt AS WorkOrderCompletedAt
            FROM dbo.Failure f
            INNER JOIN @Scope s ON s.Id = f.EquipmentId
            INNER JOIN dbo.FailureCause c ON c.Id = f.CauseId
            LEFT JOIN dbo.WorkOrder w ON w.FailureId = f.Id AND w.Status <> 7
            WHERE f.Status <> 4 AND (f.ReportedAt >= @Since OR f.Status IN (1, 2))
            ORDER BY f.ReportedAt;

            SELECT w.Id, w.Number, w.EquipmentId, CAST(0 AS bit) AS IsPreventive, w.FailureId,
                   w.MaintenancePlanId, w.DueDate, w.Status, w.AssignedTechnicianId, w.CreatedAt,
                   w.ActualStartAt, w.CompletedAt, w.ClosedAt, w.LaborMinutes, CAST(0 AS decimal(18,2)) AS PartsCost
            FROM dbo.WorkOrder w
            INNER JOIN @Scope s ON s.Id = w.EquipmentId
            WHERE w.WorkOrderType = 1 AND w.CompletedAt >= @Since AND w.ActualStartAt IS NOT NULL
            ORDER BY w.CompletedAt;

            SELECT p.Id AS SparePartId, p.Code, p.Name, p.StockQuantity, p.MinimumStock,
                   w.Id AS WorkOrderId, w.Number AS WorkOrderNumber, w.CompletedAt
            FROM dbo.WorkOrderPart wp
            INNER JOIN dbo.WorkOrder w ON w.Id = wp.WorkOrderId
            INNER JOIN @Scope s ON s.Id = w.EquipmentId
            INNER JOIN dbo.SparePart p ON p.Id = wp.SparePartId
            WHERE w.CompletedAt >= @Since
            ORDER BY p.Code, w.CompletedAt;

            SELECT mp.Id AS MaintenancePlanId, mp.EquipmentId, mp.Name AS PlanName,
                   mp.NextDueDate AS DueDate, CAST(NULL AS nvarchar(50)) AS OpenWorkOrderNumber
            FROM dbo.MaintenancePlan mp
            INNER JOIN @Scope s ON s.Id = mp.EquipmentId
            WHERE mp.IsActive = 1 AND mp.NextDueDate < @Today
            UNION ALL
            SELECT w.MaintenancePlanId, w.EquipmentId, mp.Name, w.DueDate, w.Number
            FROM dbo.WorkOrder w
            INNER JOIN @Scope s ON s.Id = w.EquipmentId
            INNER JOIN dbo.MaintenancePlan mp ON mp.Id = w.MaintenancePlanId
            WHERE w.WorkOrderType = 2 AND w.Status IN (1, 2, 3, 4) AND w.DueDate < @Today
            ORDER BY DueDate;
            """;
        var parameters = ScopeParameters(scope);
        parameters.Add("Since", now.AddDays(-Math.Max(1, lookbackDays)));
        parameters.Add("Today", now.UtcDateTime.Date);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            ScopeSql + ScopeEquipmentSql + facts, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new AdvisorFactSet
        {
            Equipment = (await grid.ReadAsync<ScopeEquipmentDto>().ConfigureAwait(false)).ToArray(),
            Failures = (await grid.ReadAsync<FailureFactDto>().ConfigureAwait(false)).ToArray(),
            CompletedCorrectiveWorkOrders = (await grid.ReadAsync<WorkOrderFactDto>().ConfigureAwait(false)).ToArray(),
            PartReplacements = (await grid.ReadAsync<PartReplacementFactDto>().ConfigureAwait(false)).ToArray(),
            OverduePlans = (await grid.ReadAsync<OverduePlanFactDto>().ConfigureAwait(false)).ToArray(),
        };
    }

    private async Task<IReadOnlyList<T>> QueryScopedAsync<T>(
        string sql,
        DynamicParameters parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<T>(new CommandDefinition(
            ScopeSql + sql, parameters, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToArray();
    }

    private static DynamicParameters Parameters(AnalyticsScope scope, DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
        {
            throw new ArgumentException("Dönem bitişi başlangıçtan sonra olmalıdır.", nameof(to));
        }

        var parameters = ScopeParameters(scope);
        parameters.Add("From", from);
        parameters.Add("To", to);
        parameters.Add("FromDate", from.UtcDateTime.Date);
        parameters.Add("ToDate", to.UtcDateTime.Date);
        return parameters;
    }

    private static DynamicParameters ScopeParameters(AnalyticsScope scope)
    {
        var parameters = new DynamicParameters();
        parameters.Add("RootId", scope.RootEquipmentId);
        parameters.Add("IncludeDescendants", scope.IncludeDescendants);
        return parameters;
    }
}
