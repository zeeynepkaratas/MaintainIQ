using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class MaintenancePlanRepository : Repository<MaintenancePlan>, IMaintenancePlanRepository
{
    private const string ById = """
        SELECT Id, EquipmentId, Name, PeriodValue, PeriodUnit, Priority, EstimatedDurationMinutes,
               NextDueDate, IsActive, CreatedAt, UpdatedAt, RowVersion
        FROM dbo.MaintenancePlan WHERE Id=@Id;
        """;
    private const string All = """
        SELECT Id, EquipmentId, Name, PeriodValue, PeriodUnit, Priority, EstimatedDurationMinutes,
               NextDueDate, IsActive, CreatedAt, UpdatedAt, RowVersion
        FROM dbo.MaintenancePlan ORDER BY NextDueDate, Id;
        """;
    private readonly InternalCrudReader<MaintenancePlan, MaintenancePlanRow> _reader;

    public MaintenancePlanRepository(IUnitOfWork unitOfWork) : base(unitOfWork) =>
        _reader = new(unitOfWork, EntityMappers.ToDomain, ById, All);
    public override Task<MaintenancePlan?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => _reader.GetByIdAsync(id, cancellationToken);
    public override Task<IReadOnlyList<MaintenancePlan>> GetAllAsync(CancellationToken cancellationToken = default) => _reader.GetAllAsync(cancellationToken);

    public async Task<IReadOnlyList<MaintenancePlan>> GetDueAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, EquipmentId, Name, PeriodValue, PeriodUnit, Priority, EstimatedDurationMinutes,
                   NextDueDate, IsActive, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.MaintenancePlan WHERE IsActive=1 AND NextDueDate<=@Date ORDER BY NextDueDate;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<MaintenancePlanRow>(Command(
            sql, new { Date = date.ToDateTime(TimeOnly.MinValue) }, cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public override Task<int> AddAsync(MaintenancePlan x, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.MaintenancePlan
                (EquipmentId,Name,PeriodValue,PeriodUnit,Priority,EstimatedDurationMinutes,NextDueDate,IsActive,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@EquipmentId,@Name,@PeriodValue,@PeriodUnit,@Priority,@EstimatedDurationMinutes,@NextDueDate,@IsActive,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql, Params(x), cancellationToken));
    }
    public override async Task UpdateAsync(MaintenancePlan x, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.MaintenancePlan SET Name=@Name,PeriodValue=@PeriodValue,PeriodUnit=@PeriodUnit,
                Priority=@Priority,EstimatedDurationMinutes=@EstimatedDurationMinutes,NextDueDate=@NextDueDate,
                IsActive=@IsActive,UpdatedAt=@UpdatedAt WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql, Params(x), cancellationToken)).ConfigureAwait(false), nameof(MaintenancePlan));
    }
    public override async Task DeleteAsync(MaintenancePlan x, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE dbo.MaintenancePlan WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql, new { x.Id, x.RowVersion }, cancellationToken)).ConfigureAwait(false), nameof(MaintenancePlan));
    }
    private static object Params(MaintenancePlan x) => new
    {
        x.Id,x.EquipmentId,x.Name,x.PeriodValue,PeriodUnit=(byte)x.PeriodUnit,Priority=(byte)x.Priority,
        x.EstimatedDurationMinutes,NextDueDate=x.NextDueDate.ToDateTime(TimeOnly.MinValue),x.IsActive,
        x.CreatedAt,x.UpdatedAt,x.RowVersion,
    };
}
