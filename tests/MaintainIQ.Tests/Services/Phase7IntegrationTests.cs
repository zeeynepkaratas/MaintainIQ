using Dapper;
using MaintainIQ.Data.Connection;
using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Maintenance;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using MaintainIQ.Services.WorkOrders;
using MaintainIQ.Tests.Data;
using Microsoft.Data.SqlClient;

namespace MaintainIQ.Tests.Services;

[Collection(DataCollection.Name)]
[Trait("Category", "SqlServerIntegration")]
public sealed class Phase7IntegrationTests
{
    private static readonly DateTimeOffset PeriodStart = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2025, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly SqlConnectionFactory _connectionFactory = new(DataFixture.ConnectionString);

    [Fact]
    public async Task PlanEngine_SecondRunOnSameDay_DoesNotCreateDuplicateWorkOrder()
    {
        var ids = await CreateHierarchyAsync();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var planId = await InsertPlanAsync(ids.ChildId, today);
            var runAt = DateTimeOffset.Now;
            var service = PlanService(ids.PersonId, runAt);

            var first = await service.GenerateDueWorkOrdersAsync(runAt);
            var second = await service.GenerateDueWorkOrdersAsync(runAt.AddMinutes(1));

            Assert.Contains(first.Items, item => item.MaintenancePlanId == planId && item.Outcome == PlanGenerationOutcome.Created);
            Assert.DoesNotContain(second.Items, item => item.MaintenancePlanId == planId && item.Outcome == PlanGenerationOutcome.Created);
            await using var connection = await OpenAsync();
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.WorkOrder WHERE MaintenancePlanId=@PlanId;", new { PlanId = planId }));
            var nextDue = await connection.ExecuteScalarAsync<DateTime>(
                "SELECT NextDueDate FROM dbo.MaintenancePlan WHERE Id=@PlanId;", new { PlanId = planId });
            Assert.Equal(today.AddMonths(1), DateOnly.FromDateTime(nextDue));

            // Plan elle eski vadesine döndürülse bile kod kontrolü kopya üretmez; DB indeksi de korur.
            await connection.ExecuteAsync(
                "UPDATE dbo.MaintenancePlan SET NextDueDate=@Today WHERE Id=@PlanId;",
                new { Today = today.ToDateTime(TimeOnly.MinValue), PlanId = planId });
            var third = await service.GenerateDueWorkOrdersAsync(runAt.AddMinutes(2));
            Assert.Contains(third.Items, item => item.MaintenancePlanId == planId && item.Outcome == PlanGenerationOutcome.AlreadyExists);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.WorkOrder WHERE MaintenancePlanId=@PlanId;", new { PlanId = planId }));
            var duplicate = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
                """
                INSERT dbo.WorkOrder (WorkOrderType,Number,EquipmentId,MaintenancePlanId,DueDate,Status,Priority,Description,CreatedAt,UpdatedAt)
                VALUES (2,@Number,@EquipmentId,@PlanId,@Today,1,2,N'Kopya',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
                """,
                new { Number = $"WO-DUP-{ids.Suffix}", EquipmentId = ids.ChildId, PlanId = planId, Today = today.ToDateTime(TimeOnly.MinValue) }));
            Assert.Equal(2601, duplicate.Number);
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    [Fact]
    public async Task CompletingGeneratedPreventiveOrder_UpdatesPlanLastDoneAt()
    {
        var ids = await CreateHierarchyAsync();
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var planId = await InsertPlanAsync(ids.ChildId, today);
            var now = DateTimeOffset.Now;
            var result = await PlanService(ids.PersonId, now).GenerateDueWorkOrdersAsync(now);
            var workOrderId = result.Items.Single(item => item.MaintenancePlanId == planId).WorkOrderId!.Value;
            var current = CurrentUser(ids.PersonId);
            var workOrders = new WorkOrderService(
                new ServiceDataSessionFactory(_connectionFactory), current, new PermissionService(current),
                new FixedClock(now.AddMinutes(1)));

            await workOrders.AssignAsync(workOrderId, ids.PersonId,
                new MaintainIQ.Domain.ValueObjects.DateRange(now.AddHours(1), now.AddHours(2)), false);
            await workOrders.StartAsync(workOrderId);
            await workOrders.CompleteAsync(workOrderId, 30);

            await using var connection = await OpenAsync();
            var lastDone = await connection.ExecuteScalarAsync<DateTimeOffset?>(
                "SELECT LastDoneAt FROM dbo.MaintenancePlan WHERE Id=@PlanId;", new { PlanId = planId });
            Assert.NotNull(lastDone);
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    [Fact]
    public async Task ReportQueries_UseRecursiveScopeAndWindowFunctions()
    {
        var ids = await CreateHierarchyAsync();
        try
        {
            await SeedAnalyticsAsync(ids);
            var reports = new ReportQueries(_connectionFactory);

            var subtree = await reports.GetKpiFactsAsync(AnalyticsScope.Subtree(ids.RootId), PeriodStart, PeriodEnd);
            var single = await reports.GetKpiFactsAsync(AnalyticsScope.Single(ids.ChildId), PeriodStart, PeriodEnd);
            var other = await reports.GetKpiFactsAsync(AnalyticsScope.Single(ids.OtherId), PeriodStart, PeriodEnd);

            Assert.Equal(3, subtree.Equipment.Count);
            Assert.Equal(4, subtree.Failures.Count);
            Assert.Single(single.Equipment);
            Assert.Equal(3, single.Failures.Count);
            Assert.Empty(other.Failures);
            var repaired = Assert.Single(single.WorkOrders);
            Assert.Equal(40m, repaired.PartsCost);
            Assert.False(repaired.IsPreventive);

            var pareto = await reports.GetFailureParetoAsync(AnalyticsScope.Subtree(ids.RootId), PeriodStart, PeriodEnd);
            Assert.Equal(new[] { "MECHANICAL", "ELECTRICAL" }, pareto.Select(p => p.CauseCode).ToArray());
            Assert.Equal(75m, pareto[0].SharePercent);
            Assert.Equal(100m, pareto[1].CumulativePercent);

            var trend = await reports.GetMonthlyFailureTrendAsync(
                AnalyticsScope.Subtree(ids.RootId), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), PeriodEnd);
            Assert.Equal(new[] { 0, 0, 4 }, trend.Select(t => t.FailureCount).ToArray());
            Assert.Equal(1.33m, trend[2].MovingAverage3);

            var parts = await reports.GetTopPartConsumptionAsync(AnalyticsScope.Subtree(ids.RootId), PeriodStart, PeriodEnd, 5);
            var top = Assert.Single(parts);
            Assert.Equal(1, top.Rank);
            Assert.Equal(2m, top.TotalQuantity);

            var costs = await reports.GetEquipmentCostsAsync(AnalyticsScope.Subtree(ids.RootId), PeriodStart, PeriodEnd);
            Assert.Equal(3, costs.Count);
            Assert.Equal(40m, costs[0].PartsCost);
            Assert.Equal(90, costs[0].LaborMinutes);

            var workload = await reports.GetTechnicianWorkloadAsync(PeriodStart, PeriodEnd);
            Assert.Contains(workload, w => w.TechnicianId == ids.PersonId && w.CompletedInPeriod == 1 && w.LaborHoursInPeriod == 1.5m);
            Assert.True(await reports.GetCriticalStockCountAsync() >= 1);

            var advisor = await reports.GetAdvisorFactsAsync(
                AnalyticsScope.Subtree(ids.RootId), new DateTimeOffset(2025, 4, 10, 0, 0, 0, TimeSpan.Zero), 183);
            Assert.Equal(4, advisor.Failures.Count);
            Assert.Single(advisor.CompletedCorrectiveWorkOrders);
            Assert.Single(advisor.PartReplacements);
            Assert.Contains(advisor.Equipment, e => e.Id == ids.ChildId && e.IsCritical);
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    [Fact]
    public async Task Repositories_RoundTripCriticalityWarrantyAnchorAndQrLookup()
    {
        var ids = await CreateHierarchyAsync();
        try
        {
            await using var uow = new UnitOfWork(_connectionFactory);
            await uow.BeginAsync();
            var equipmentRepository = new EquipmentRepository(uow);
            var equipment = (await equipmentRepository.GetByIdAsync(ids.ChildId))!;
            Assert.True(equipment.IsCritical);
            Assert.Equal(new DateOnly(2025, 5, 1), equipment.WarrantyEndDate);
            Assert.Equal(ids.ChildId, (await equipmentRepository.GetByQrTokenAsync($"tok{ids.Suffix}"))!.Id);
            Assert.Null(await equipmentRepository.GetByQrTokenAsync("missing-token"));

            var plans = new MaintenancePlanRepository(uow);
            var plan = new MaintainIQ.Domain.Entities.MaintenancePlan(
                ids.ChildId, "Yıllık kalibrasyon", 1, MaintenancePeriodUnit.Year, Priority.Normal, 120,
                new DateOnly(2025, 1, 31), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var planId = await plans.AddAsync(plan);
            var loaded = (await plans.GetByIdAsync(planId))!;
            loaded.RecordCompletion(new DateTimeOffset(2025, 2, 1, 10, 0, 0, TimeSpan.Zero));
            await plans.UpdateAsync(loaded);
            var reloaded = (await plans.GetForEquipmentAsync(ids.ChildId)).Single();
            Assert.Equal(new DateOnly(2025, 1, 31), reloaded.AnchorDate);
            Assert.Equal(new DateOnly(2026, 1, 31), reloaded.NextDueDate);
            Assert.NotNull(reloaded.LastDoneAt);
            await uow.RollbackAsync();
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    private MaintenancePlanService PlanService(int personId, DateTimeOffset now)
    {
        var current = CurrentUser(personId);
        return new MaintenancePlanService(
            new ServiceDataSessionFactory(_connectionFactory), current, new PermissionService(current),
            new FixedClock(now), new WorkOrderNumberGenerator());
    }

    private async Task<int> InsertPlanAsync(int equipmentId, DateOnly dueDate)
    {
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.MaintenancePlan (EquipmentId,Name,PeriodValue,PeriodUnit,Priority,EstimatedDurationMinutes,NextDueDate,IsActive,AnchorDate,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@EquipmentId,N'Entegrasyon planı',1,3,2,60,@Due,1,@Due,@Created,@Created);
            """,
            new { EquipmentId = equipmentId, Due = dueDate.ToDateTime(TimeOnly.MinValue), Created = DateTimeOffset.Now.AddDays(-60) });
    }

    private async Task SeedAnalyticsAsync(HierarchyIds ids)
    {
        await using var connection = await OpenAsync();
        var mechanical = await connection.ExecuteScalarAsync<int>("SELECT Id FROM dbo.FailureCause WHERE Code='MECHANICAL';");
        var electrical = await connection.ExecuteScalarAsync<int>("SELECT Id FROM dbo.FailureCause WHERE Code='ELECTRICAL';");
        async Task<int> FailureAsync(int equipmentId, int causeId, DateTimeOffset at) =>
            await connection.ExecuteScalarAsync<int>(
                """
                INSERT dbo.Failure (EquipmentId,CauseId,Severity,Status,Description,ReportedByPersonId,ReportedAt,ResolvedAt,CreatedAt,UpdatedAt)
                OUTPUT INSERTED.Id VALUES (@EquipmentId,@CauseId,2,3,N'Analiz',@PersonId,@At,DATEADD(hour,4,@At),@At,DATEADD(hour,4,@At));
                """,
                new { EquipmentId = equipmentId, CauseId = causeId, ids.PersonId, At = at });
        var first = await FailureAsync(ids.ChildId, mechanical, new DateTimeOffset(2025, 3, 2, 8, 0, 0, TimeSpan.Zero));
        await FailureAsync(ids.ChildId, mechanical, new DateTimeOffset(2025, 3, 9, 8, 0, 0, TimeSpan.Zero));
        await FailureAsync(ids.ChildId, electrical, new DateTimeOffset(2025, 3, 16, 8, 0, 0, TimeSpan.Zero));
        await FailureAsync(ids.GrandchildId, mechanical, new DateTimeOffset(2025, 3, 20, 8, 0, 0, TimeSpan.Zero));
        await FailureAsync(ids.OtherId, mechanical, new DateTimeOffset(2025, 2, 20, 8, 0, 0, TimeSpan.Zero));

        var workOrderId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.WorkOrder (WorkOrderType,Number,EquipmentId,FailureId,Status,Priority,Description,AssignedTechnicianId,
                                  ActualStartAt,CompletedAt,ClosedAt,LaborMinutes,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (1,@Number,@EquipmentId,@FailureId,6,3,N'Analiz onarımı',@PersonId,
                    '2025-03-02T09:00:00+00:00','2025-03-02T10:30:00+00:00','2025-03-03T08:00:00+00:00',90,
                    '2025-03-02T08:30:00+00:00','2025-03-03T08:00:00+00:00');
            """,
            new { Number = $"WO-AN-{ids.Suffix}", EquipmentId = ids.ChildId, FailureId = first, ids.PersonId });
        await connection.ExecuteAsync(
            """
            INSERT dbo.WorkOrderPart (WorkOrderId,SparePartId,Quantity,UnitCostSnapshot,Currency,CreatedAt,UpdatedAt)
            VALUES (@WorkOrderId,@SparePartId,2,20,'TRY',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
            """,
            new { WorkOrderId = workOrderId, ids.SparePartId });
    }

    private async Task<HierarchyIds> CreateHierarchyAsync()
    {
        await using var connection = await OpenAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow.AddYears(-2);
        var personId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.Person (PersonType,EmployeeNumber,FirstName,LastName,Specialty,IsActive,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (3,@EmployeeNumber,N'Faz7',N'Teknisyen',N'Mekanik',1,@Now,@Now);
            """,
            new { EmployeeNumber = $"P7-{suffix}", Now = now });
        async Task<int> EquipmentAsync(string code, int? parentId, bool critical, DateTime? warranty, string? token) =>
            await connection.ExecuteScalarAsync<int>(
                """
                INSERT dbo.Equipment (ParentId,Code,Name,Location,QrToken,Status,IsDeleted,IsCritical,WarrantyEndDate,CreatedAt,UpdatedAt)
                OUTPUT INSERTED.Id VALUES (@ParentId,@Code,N'Faz 7 ekipmanı',N'Hat',@Token,1,0,@Critical,@Warranty,@Now,@Now);
                """,
                new { ParentId = parentId, Code = code, Critical = critical, Warranty = warranty, Token = token, Now = now });
        var rootId = await EquipmentAsync($"P7-ROOT-{suffix}", null, false, null, null);
        var childId = await EquipmentAsync($"P7-CHILD-{suffix}", rootId, true, new DateTime(2025, 5, 1), $"tok{suffix}");
        var grandchildId = await EquipmentAsync($"P7-GRAND-{suffix}", childId, false, null, null);
        var otherId = await EquipmentAsync($"P7-OTHER-{suffix}", null, false, null, null);
        var sparePartId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.SparePart (Code,Name,StockQuantity,MinimumStock,UnitCost,Currency,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@Code,N'Faz 7 parçası',1,5,20,'TRY',@Now,@Now);
            """,
            new { Code = $"P7-P-{suffix}", Now = now });
        return new HierarchyIds(suffix, personId, rootId, childId, grandchildId, otherId, sparePartId);
    }

    private async Task CleanupAsync(HierarchyIds ids)
    {
        await using var connection = await OpenAsync();
        await connection.ExecuteAsync(
            """
            DECLARE @Equipment TABLE (Id int PRIMARY KEY);
            INSERT @Equipment VALUES (@GrandchildId), (@ChildId), (@RootId), (@OtherId);
            DECLARE @Orders TABLE (Id int PRIMARY KEY);
            INSERT @Orders SELECT Id FROM dbo.WorkOrder WHERE EquipmentId IN (SELECT Id FROM @Equipment);
            DELETE dbo.AuditLog WHERE PerformedByPersonId=@PersonId;
            DELETE dbo.StockMovement WHERE WorkOrderId IN (SELECT Id FROM @Orders) OR SparePartId=@SparePartId;
            DELETE dbo.WorkOrderStatusHistory WHERE WorkOrderId IN (SELECT Id FROM @Orders);
            DELETE dbo.WorkOrderPart WHERE WorkOrderId IN (SELECT Id FROM @Orders);
            DELETE dbo.WorkOrder WHERE Id IN (SELECT Id FROM @Orders);
            DELETE dbo.Failure WHERE EquipmentId IN (SELECT Id FROM @Equipment);
            DELETE dbo.MaintenancePlan WHERE EquipmentId IN (SELECT Id FROM @Equipment);
            DELETE dbo.SparePart WHERE Id=@SparePartId;
            DELETE dbo.Equipment WHERE Id=@GrandchildId;
            DELETE dbo.Equipment WHERE Id=@ChildId;
            DELETE dbo.Equipment WHERE Id IN (@RootId, @OtherId);
            DELETE dbo.Person WHERE Id=@PersonId;
            """,
            ids);
    }

    private async Task<SqlConnection> OpenAsync() => await _connectionFactory.OpenConnectionAsync();

    private static CurrentUserContext CurrentUser(int personId)
    {
        var current = new CurrentUserContext();
        current.SignIn(1, personId, "phase7", UserRole.Admin);
        return current;
    }

    private sealed record HierarchyIds(
        string Suffix, int PersonId, int RootId, int ChildId, int GrandchildId, int OtherId, int SparePartId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
