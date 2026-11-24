using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Services.Auditing;
using MaintainIQ.Services.Common;
using MaintainIQ.Services.Persistence;
using MaintainIQ.Services.Security;
using MaintainIQ.Services.WorkOrders;
using Microsoft.Data.SqlClient;

namespace MaintainIQ.Services.Maintenance;

public sealed record CreateMaintenancePlanCommand(
    int EquipmentId,
    string Name,
    int PeriodValue,
    MaintenancePeriodUnit PeriodUnit,
    Priority Priority,
    int EstimatedDurationMinutes,
    DateOnly FirstDueDate);

public sealed record UpdateMaintenancePlanCommand(
    int Id,
    string Name,
    int PeriodValue,
    MaintenancePeriodUnit PeriodUnit,
    Priority Priority,
    int EstimatedDurationMinutes,
    DateOnly NextDueDate);

public enum PlanGenerationOutcome
{
    Created = 1,
    AlreadyExists = 2,
    Failed = 3,
}

public sealed record PlanGenerationItem(
    int MaintenancePlanId,
    DateOnly DueDate,
    PlanGenerationOutcome Outcome,
    int? WorkOrderId = null,
    string? WorkOrderNumber = null,
    int SkippedOccurrences = 0,
    string? Message = null);

public sealed record PlanGenerationResult(DateTimeOffset RunAt, IReadOnlyList<PlanGenerationItem> Items)
{
    public int CreatedCount => Items.Count(item => item.Outcome == PlanGenerationOutcome.Created);
    public int AlreadyExistingCount => Items.Count(item => item.Outcome == PlanGenerationOutcome.AlreadyExists);
    public int FailedCount => Items.Count(item => item.Outcome == PlanGenerationOutcome.Failed);
}

public interface IMaintenancePlanService
{
    Task<int> CreateAsync(CreateMaintenancePlanCommand command, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateMaintenancePlanCommand command, CancellationToken cancellationToken = default);
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);
    Task<MaintenancePlan> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenancePlan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenancePlan>> GetForEquipmentAsync(int equipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vadesi gelmiş veya gecikmiş aktif planlardan önleyici iş emri üretir. İdempotenttir:
    /// aynı plan + vade için ikinci iş emri üretilmez (kod kontrolü + UX_WorkOrder_PreventiveOccurrence).
    /// </summary>
    Task<PlanGenerationResult> GenerateDueWorkOrdersAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

public sealed class MaintenancePlanService(
    IServiceDataSessionFactory sessionFactory,
    ICurrentUser currentUser,
    IPermissionService permissions,
    IClock clock,
    IWorkOrderNumberGenerator numberGenerator) : IMaintenancePlanService
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private readonly AuditWriter _audit = new(currentUser);

    public Task<int> CreateAsync(CreateMaintenancePlanCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageMaintenancePlan);
        return ServiceTransaction.RunAsync(sessionFactory, async session =>
        {
            var equipment = await session.Equipment.GetByIdAsync(command.EquipmentId, cancellationToken).ConfigureAwait(false);
            if (equipment is null || equipment.IsDeleted)
            {
                throw new EntityNotFoundException(nameof(Domain.Entities.Equipment), command.EquipmentId);
            }

            if (equipment.Status == EquipmentStatus.Retired)
            {
                throw new BusinessRuleException("PM-RETIRED", "Hizmet dışı ekipman için bakım planı oluşturulamaz.");
            }

            var now = clock.UtcNow;
            var plan = new MaintenancePlan(
                command.EquipmentId, command.Name, command.PeriodValue, command.PeriodUnit,
                command.Priority, command.EstimatedDurationMinutes, command.FirstDueDate, now);
            var id = await session.MaintenancePlans.AddAsync(plan, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(MaintenancePlan), id.ToString(), "Created", null, Snapshot(plan),
                now, cancellationToken).ConfigureAwait(false);
            return id;
        }, cancellationToken);
    }

    public Task UpdateAsync(UpdateMaintenancePlanCommand command, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageMaintenancePlan);
        return MutateAsync(command.Id, "Updated", (plan, now) => plan.Update(
            command.Name, command.PeriodValue, command.PeriodUnit, command.Priority,
            command.EstimatedDurationMinutes, command.NextDueDate, now), cancellationToken);
    }

    public Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageMaintenancePlan);
        return MutateAsync(id, "Activated", (plan, now) =>
        {
            plan.Activate();
            plan.MarkUpdated(now);
        }, cancellationToken);
    }

    public Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        permissions.Ensure(Permission.ManageMaintenancePlan);
        return MutateAsync(id, "Deactivated", (plan, now) =>
        {
            plan.Deactivate();
            plan.MarkUpdated(now);
        }, cancellationToken);
    }

    public Task<MaintenancePlan> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        return ServiceTransaction.RunAsync(
            sessionFactory, session => RequireAsync(session, id, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<MaintenancePlan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        return ServiceTransaction.RunAsync(
            sessionFactory, session => session.MaintenancePlans.GetAllAsync(cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<MaintenancePlan>> GetForEquipmentAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        return ServiceTransaction.RunAsync(
            sessionFactory,
            session => session.MaintenancePlans.GetForEquipmentAsync(equipmentId, cancellationToken),
            cancellationToken);
    }

    public async Task<PlanGenerationResult> GenerateDueWorkOrdersAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        EnsureAuthenticated();
        var today = DateOnly.FromDateTime(now.DateTime);
        var duePlans = await ServiceTransaction.RunAsync(
            sessionFactory,
            session => session.MaintenancePlans.GetDueAsync(today, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var items = new List<PlanGenerationItem>(duePlans.Count);
        foreach (var duePlan in duePlans)
        {
            items.Add(await GenerateForPlanAsync(duePlan.Id, duePlan.NextDueDate, today, now, cancellationToken)
                .ConfigureAwait(false));
        }

        return new PlanGenerationResult(now, items);
    }

    private async Task<PlanGenerationItem> GenerateForPlanAsync(
        int planId,
        DateOnly expectedDueDate,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceTransaction.RunAsync(sessionFactory, async session =>
            {
                var plan = await RequireAsync(session, planId, cancellationToken).ConfigureAwait(false);
                if (!plan.IsDue(today))
                {
                    // Başka bir çalıştırma bu vadeyi zaten işledi.
                    return new PlanGenerationItem(planId, expectedDueDate, PlanGenerationOutcome.AlreadyExists);
                }

                var dueDate = plan.NextDueDate;
                var exists = await session.WorkOrders.ExistsPreventiveOccurrenceAsync(
                    plan.Id, dueDate, cancellationToken).ConfigureAwait(false);
                var before = Snapshot(plan);
                int? workOrderId = null;
                string? number = null;
                if (!exists)
                {
                    var order = new PreventiveWorkOrder(
                        numberGenerator.Next(now), plan.EquipmentId, plan.Id, dueDate, plan.Priority,
                        $"{plan.Name} - planlı bakım (vade {dueDate:dd.MM.yyyy})", now);
                    workOrderId = await session.WorkOrders.AddAsync(order, cancellationToken).ConfigureAwait(false);
                    number = order.Number;
                    await _audit.WriteAsync(
                        session, nameof(WorkOrder), workOrderId.Value.ToString(), "GeneratedFromPlan", null,
                        new { MaintenancePlanId = plan.Id, DueDate = dueDate, order.EquipmentId, order.Number, order.Priority },
                        now, cancellationToken).ConfigureAwait(false);
                }

                var skipped = plan.MarkOccurrenceGenerated(today, now);
                await session.MaintenancePlans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
                await _audit.WriteAsync(
                    session, nameof(MaintenancePlan), plan.Id.ToString(), "DueDateAdvanced",
                    before, Snapshot(plan), now, cancellationToken).ConfigureAwait(false);

                return exists
                    ? new PlanGenerationItem(plan.Id, dueDate, PlanGenerationOutcome.AlreadyExists, SkippedOccurrences: skipped)
                    : new PlanGenerationItem(plan.Id, dueDate, PlanGenerationOutcome.Created, workOrderId, number, skipped);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolation or UniqueConstraintViolation)
        {
            return new PlanGenerationItem(planId, expectedDueDate, PlanGenerationOutcome.AlreadyExists,
                Message: "Aynı plan ve vade için iş emri eşzamanlı olarak oluşturuldu.");
        }
        catch (DomainException exception)
        {
            return new PlanGenerationItem(planId, expectedDueDate, PlanGenerationOutcome.Failed, Message: exception.Message);
        }
    }

    private Task MutateAsync(
        int id,
        string action,
        Action<MaintenancePlan, DateTimeOffset> mutation,
        CancellationToken cancellationToken) =>
        ServiceTransaction.RunAsync(sessionFactory, async session =>
        {
            var plan = await RequireAsync(session, id, cancellationToken).ConfigureAwait(false);
            var before = Snapshot(plan);
            var now = clock.UtcNow;
            mutation(plan, now);
            await session.MaintenancePlans.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
            await _audit.WriteAsync(
                session, nameof(MaintenancePlan), plan.Id.ToString(), action, before, Snapshot(plan),
                now, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new PermissionDeniedException("Bu işlem için oturum açılmalıdır.");
        }
    }

    private static async Task<MaintenancePlan> RequireAsync(
        IServiceDataSession session,
        int id,
        CancellationToken cancellationToken) =>
        await session.MaintenancePlans.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
        ?? throw new EntityNotFoundException(nameof(MaintenancePlan), id);

    private static object Snapshot(MaintenancePlan plan) => new
    {
        plan.EquipmentId,
        plan.Name,
        plan.PeriodValue,
        plan.PeriodUnit,
        plan.Priority,
        plan.EstimatedDurationMinutes,
        plan.NextDueDate,
        plan.AnchorDate,
        plan.LastDoneAt,
        plan.IsActive,
    };
}
