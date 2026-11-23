using Dapper;
using MaintainIQ.Data.Connection;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using MaintainIQ.Services.WorkOrders;
using MaintainIQ.Tests.Data;
using Microsoft.Data.SqlClient;

namespace MaintainIQ.Tests.Services;

[Collection(DataCollection.Name)]
[Trait("Category", "SqlServerIntegration")]
public sealed class ServiceIntegrationTests
{
    private readonly SqlConnectionFactory _connectionFactory = new(DataFixture.ConnectionString);

    [Fact]
    public async Task WorkOrderLifecycle_RunsWithoutUi_AndCommitsAuditWithBusinessData()
    {
        var ids = await CreateScenarioAsync(WorkOrderStatus.Created, includePart: false);
        try
        {
            var current = CurrentUser(ids.PersonId);
            var service = new WorkOrderService(
                new ServiceDataSessionFactory(_connectionFactory),
                current,
                new PermissionService(current),
                new FixedClock(ids.Now.AddMinutes(1)));

            await service.AssignAsync(
                ids.WorkOrderId,
                ids.PersonId,
                new DateRange(ids.Now.AddHours(1), ids.Now.AddHours(2)),
                false);
            await service.StartAsync(ids.WorkOrderId);
            await service.AddPartAsync(ids.WorkOrderId, ids.SparePartId, 2m);
            await service.CompleteAsync(ids.WorkOrderId, 60);
            await service.CloseAsync(ids.WorkOrderId);

            await using var connection = new SqlConnection(DataFixture.ConnectionString);
            await connection.OpenAsync();
            var state = await connection.QuerySingleAsync<(byte Status, decimal StockQuantity)>(
                """
                SELECT w.Status, p.StockQuantity
                FROM dbo.WorkOrder w
                CROSS JOIN dbo.SparePart p
                WHERE w.Id=@WorkOrderId AND p.Id=@SparePartId;
                """,
                ids);
            Assert.Equal((byte)WorkOrderStatus.Closed, state.Status);
            Assert.Equal(1m, state.StockQuantity);
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockMovement WHERE WorkOrderId=@WorkOrderId;", ids));
            Assert.Equal(5, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName='WorkOrder' AND EntityId=@EntityId;",
                new { EntityId = ids.WorkOrderId.ToString() }));
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    [Fact]
    public async Task Complete_WhenAuditWriteFails_RollsBackStockStatusMovementAndEquipment()
    {
        var ids = await CreateScenarioAsync(WorkOrderStatus.InProgress, includePart: true);
        try
        {
            var current = CurrentUser(ids.PersonId);
            var service = new WorkOrderService(
                new ThrowingAuditSessionFactory(_connectionFactory),
                current,
                new PermissionService(current),
                new FixedClock(ids.Now.AddMinutes(1)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CompleteAsync(ids.WorkOrderId, 60));

            await using var connection = new SqlConnection(DataFixture.ConnectionString);
            await connection.OpenAsync();
            var state = await connection.QuerySingleAsync<(byte Status, decimal StockQuantity, byte EquipmentStatus)>(
                """
                SELECT w.Status, p.StockQuantity, e.Status AS EquipmentStatus
                FROM dbo.WorkOrder w
                CROSS JOIN dbo.SparePart p
                INNER JOIN dbo.Equipment e ON e.Id=w.EquipmentId
                WHERE w.Id=@WorkOrderId AND p.Id=@SparePartId;
                """,
                ids);
            Assert.Equal((byte)WorkOrderStatus.InProgress, state.Status);
            Assert.Equal(3m, state.StockQuantity);
            Assert.Equal((byte)EquipmentStatus.Faulty, state.EquipmentStatus);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockMovement WHERE WorkOrderId=@WorkOrderId;", ids));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityName='WorkOrder' AND EntityId=@EntityId;",
                new { EntityId = ids.WorkOrderId.ToString() }));
        }
        finally
        {
            await CleanupAsync(ids);
        }
    }

    [Fact]
    public async Task ConcurrentComplete_OnLastThreeItems_AllowsOnlyOneConsumption()
    {
        var ids = await CreateScenarioAsync(WorkOrderStatus.InProgress, includePart: true);
        var secondWorkOrderId = 0;
        var secondFailureId = 0;
        try
        {
            await using (var connection = new SqlConnection(DataFixture.ConnectionString))
            {
                await connection.OpenAsync();
                secondFailureId = await connection.ExecuteScalarAsync<int>(
                    """
                    INSERT dbo.Failure
                        (EquipmentId,CauseId,Severity,Status,Description,ReportedByPersonId,ReportedAt,CreatedAt,UpdatedAt)
                    OUTPUT INSERTED.Id
                    SELECT @EquipmentId,CauseId,Severity,Status,N'Eşzamanlı servis testi',@PersonId,
                           @ReportedAt,@ReportedAt,@ReportedAt
                    FROM dbo.Failure WHERE Id=@SourceFailureId;
                    """,
                    new
                    {
                        ids.EquipmentId,
                        ids.PersonId,
                        ReportedAt = ids.Now.AddHours(-2),
                        SourceFailureId = ids.FailureId,
                    });
                secondWorkOrderId = await connection.ExecuteScalarAsync<int>(
                    """
                    INSERT dbo.WorkOrder
                        (WorkOrderType,Number,EquipmentId,FailureId,Status,Priority,Description,
                         AssignedTechnicianId,ActualStartAt,LaborMinutes,CreatedAt,UpdatedAt)
                    OUTPUT INSERTED.Id
                    VALUES (1,@Number,@EquipmentId,@FailureId,3,3,N'Eşzamanlı servis testi',
                            @PersonId,@ActualStartAt,0,@CreatedAt,@UpdatedAt);
                    """,
                    new
                    {
                        Number = $"WO-CON-{Guid.NewGuid():N}"[..20],
                        ids.EquipmentId,
                        FailureId = secondFailureId,
                        ids.PersonId,
                        ActualStartAt = ids.Now.AddHours(-1),
                        CreatedAt = ids.Now.AddHours(-2),
                        UpdatedAt = ids.Now.AddHours(-1),
                    });
                await connection.ExecuteAsync(
                    """
                    INSERT dbo.WorkOrderPart
                        (WorkOrderId,SparePartId,Quantity,UnitCostSnapshot,Currency,CreatedAt,UpdatedAt)
                    VALUES (@WorkOrderId,@SparePartId,2,25,'TRY',@Now,@Now);
                    """,
                    new { WorkOrderId = secondWorkOrderId, ids.SparePartId, ids.Now });
            }

            var current = CurrentUser(ids.PersonId);
            var service = new WorkOrderService(
                new ServiceDataSessionFactory(_connectionFactory),
                current,
                new PermissionService(current),
                new FixedClock(ids.Now.AddMinutes(1)));

            var attempts = await Task.WhenAll(
                Record.ExceptionAsync(() => service.CompleteAsync(ids.WorkOrderId, 60)),
                Record.ExceptionAsync(() => service.CompleteAsync(secondWorkOrderId, 60)));

            Assert.Single(attempts, error => error is null);
            Assert.Single(attempts, error => error is BusinessRuleException { RuleCode: "BR-02" });

            await using var verification = new SqlConnection(DataFixture.ConnectionString);
            await verification.OpenAsync();
            Assert.Equal(1m, await verification.ExecuteScalarAsync<decimal>(
                "SELECT StockQuantity FROM dbo.SparePart WHERE Id=@SparePartId;", ids));
            var statuses = (await verification.QueryAsync<byte>(
                "SELECT Status FROM dbo.WorkOrder WHERE Id IN (@First,@Second) ORDER BY Id;",
                new { First = ids.WorkOrderId, Second = secondWorkOrderId })).ToArray();
            Assert.Contains((byte)WorkOrderStatus.Completed, statuses);
            Assert.Contains((byte)WorkOrderStatus.InProgress, statuses);
            Assert.Equal(1, await verification.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.StockMovement WHERE SparePartId=@SparePartId;", ids));
        }
        finally
        {
            if (secondWorkOrderId > 0)
            {
                await using var connection = new SqlConnection(DataFixture.ConnectionString);
                await connection.OpenAsync();
                await connection.ExecuteAsync(
                    """
                    DELETE dbo.AuditLog WHERE EntityName='WorkOrder' AND EntityId=@EntityId;
                    DELETE dbo.StockMovement WHERE WorkOrderId=@WorkOrderId;
                    DELETE dbo.WorkOrderStatusHistory WHERE WorkOrderId=@WorkOrderId;
                    DELETE dbo.WorkOrderPart WHERE WorkOrderId=@WorkOrderId;
                    DELETE dbo.WorkOrder WHERE Id=@WorkOrderId;
                    DELETE dbo.Failure WHERE Id=@FailureId;
                    """,
                    new
                    {
                        EntityId = secondWorkOrderId.ToString(),
                        WorkOrderId = secondWorkOrderId,
                        FailureId = secondFailureId,
                    });
            }

            await CleanupAsync(ids);
        }
    }

    private async Task<ScenarioIds> CreateScenarioAsync(WorkOrderStatus status, bool includePart)
    {
        await using var connection = new SqlConnection(DataFixture.ConnectionString);
        await connection.OpenAsync();
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var causeId = await connection.ExecuteScalarAsync<int>("SELECT TOP (1) Id FROM dbo.FailureCause ORDER BY Id;");
        var personId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.Person (PersonType,EmployeeNumber,FirstName,LastName,Specialty,IsActive,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (3,@EmployeeNumber,N'Test',N'Teknisyen',N'Mekanik',1,@Now,@Now);
            """,
            new { EmployeeNumber = $"SVC-{suffix}", Now = now });
        var equipmentId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.Equipment (Code,Name,Location,Status,IsDeleted,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@Code,N'Servis testi',N'Hat',2,0,@Now,@Now);
            """,
            new { Code = $"SVC-EQ-{suffix}", Now = now });
        var failureId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.Failure
                (EquipmentId,CauseId,Severity,Status,Description,ReportedByPersonId,ReportedAt,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@EquipmentId,@CauseId,3,1,N'Servis testi',@PersonId,@ReportedAt,@ReportedAt,@ReportedAt);
            """,
            new { EquipmentId = equipmentId, CauseId = causeId, PersonId = personId, ReportedAt = now.AddHours(-2) });
        var workOrderId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.WorkOrder
                (WorkOrderType,Number,EquipmentId,FailureId,Status,Priority,Description,
                 AssignedTechnicianId,ActualStartAt,LaborMinutes,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (1,@Number,@EquipmentId,@FailureId,@Status,3,N'Servis testi',
                    @AssignedTechnicianId,@ActualStartAt,0,@CreatedAt,@UpdatedAt);
            """,
            new
            {
                Number = $"WO-SVC-{suffix}",
                EquipmentId = equipmentId,
                FailureId = failureId,
                Status = (byte)status,
                AssignedTechnicianId = status == WorkOrderStatus.Created ? null : (int?)personId,
                ActualStartAt = status == WorkOrderStatus.InProgress ? now.AddHours(-1) : (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-2),
                UpdatedAt = now.AddHours(-1),
            });
        var sparePartId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT dbo.SparePart (Code,Name,StockQuantity,MinimumStock,UnitCost,Currency,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@Code,N'Servis parçası',3,1,25,'TRY',@Now,@Now);
            """,
            new { Code = $"SVC-P-{suffix}", Now = now });

        if (includePart)
        {
            await connection.ExecuteAsync(
                """
                INSERT dbo.WorkOrderPart
                    (WorkOrderId,SparePartId,Quantity,UnitCostSnapshot,Currency,CreatedAt,UpdatedAt)
                VALUES (@WorkOrderId,@SparePartId,2,25,'TRY',@Now,@Now);
                """,
                new { WorkOrderId = workOrderId, SparePartId = sparePartId, Now = now });
        }

        return new(now, personId, equipmentId, failureId, workOrderId, sparePartId);
    }

    private static CurrentUserContext CurrentUser(int personId)
    {
        var current = new CurrentUserContext();
        current.SignIn(1, personId, "service-test", UserRole.Admin);
        return current;
    }

    private static async Task CleanupAsync(ScenarioIds ids)
    {
        await using var connection = new SqlConnection(DataFixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            DELETE dbo.AuditLog WHERE EntityId=@EntityId OR PerformedByPersonId=@PersonId;
            DELETE dbo.StockMovement WHERE WorkOrderId=@WorkOrderId OR SparePartId=@SparePartId;
            DELETE dbo.WorkOrderStatusHistory WHERE WorkOrderId=@WorkOrderId;
            DELETE dbo.WorkOrderPart WHERE WorkOrderId=@WorkOrderId;
            DELETE dbo.WorkOrder WHERE Id=@WorkOrderId;
            DELETE dbo.Failure WHERE Id=@FailureId;
            DELETE dbo.SparePart WHERE Id=@SparePartId;
            DELETE dbo.Equipment WHERE Id=@EquipmentId;
            DELETE dbo.Person WHERE Id=@PersonId;
            """,
            new
            {
                EntityId = ids.WorkOrderId.ToString(),
                ids.PersonId,
                ids.WorkOrderId,
                ids.SparePartId,
                ids.FailureId,
                ids.EquipmentId,
            });
    }

    private sealed record ScenarioIds(
        DateTimeOffset Now,
        int PersonId,
        int EquipmentId,
        int FailureId,
        int WorkOrderId,
        int SparePartId);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class ThrowingAuditSessionFactory(IDbConnectionFactory connectionFactory)
        : IServiceDataSessionFactory
    {
        public IServiceDataSession Create() => new ThrowingAuditSession(new UnitOfWork(connectionFactory));
    }

    private sealed class ThrowingAuditSession : IServiceDataSession
    {
        public ThrowingAuditSession(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork;
            Equipment = new EquipmentRepository(unitOfWork);
            Failures = new FailureRepository(unitOfWork);
            WorkOrders = new WorkOrderRepository(unitOfWork);
            SpareParts = new SparePartRepository(unitOfWork);
            StockMovements = new StockMovementRepository(unitOfWork);
            Technicians = new TechnicianRepository(unitOfWork);
            Users = new UserRepository(unitOfWork);
            Audit = new ThrowingAuditRepository();
        }

        public IUnitOfWork UnitOfWork { get; }
        public IEquipmentRepository Equipment { get; }
        public IFailureRepository Failures { get; }
        public IWorkOrderRepository WorkOrders { get; }
        public ISparePartRepository SpareParts { get; }
        public IStockMovementRepository StockMovements { get; }
        public ITechnicianRepository Technicians { get; }
        public IUserRepository Users { get; }
        public IAuditRepository Audit { get; }
        public ValueTask DisposeAsync() => UnitOfWork.DisposeAsync();
    }

    private sealed class ThrowingAuditRepository : IAuditRepository
    {
        public Task<int> AddAsync(AuditLog entity, CancellationToken cancellationToken = default) =>
            Task.FromException<int>(new InvalidOperationException("Simulated audit failure."));
        public Task DeleteAsync(AuditLog entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AuditLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(AuditLog entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
