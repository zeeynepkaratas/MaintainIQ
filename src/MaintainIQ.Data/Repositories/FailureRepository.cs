using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class FailureRepository : Repository<Failure>, IFailureRepository
{
    private const string ById = """
        SELECT Id, EquipmentId, CauseId, Severity, Status, Description, ReportedByPersonId,
               ReportedAt, ResolvedAt, CreatedAt, UpdatedAt, RowVersion
        FROM dbo.Failure WHERE Id = @Id;
        """;
    private const string All = """
        SELECT Id, EquipmentId, CauseId, Severity, Status, Description, ReportedByPersonId,
               ReportedAt, ResolvedAt, CreatedAt, UpdatedAt, RowVersion
        FROM dbo.Failure ORDER BY ReportedAt DESC;
        """;
    private readonly InternalCrudReader<Failure, FailureRow> _reader;

    public FailureRepository(IUnitOfWork unitOfWork) : base(unitOfWork) =>
        _reader = new(unitOfWork, EntityMappers.ToDomain, ById, All);

    public override Task<Failure?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _reader.GetByIdAsync(id, cancellationToken);
    public override Task<IReadOnlyList<Failure>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _reader.GetAllAsync(cancellationToken);

    public override Task<int> AddAsync(Failure entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.Failure
                (EquipmentId, CauseId, Severity, Status, Description, ReportedByPersonId,
                 ReportedAt, ResolvedAt, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@EquipmentId, @CauseId, @Severity, @Status, @Description, @ReportedByPersonId,
                    @ReportedAt, @ResolvedAt, @CreatedAt, @UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql, Params(entity), cancellationToken));
    }

    public override async Task UpdateAsync(Failure entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Failure SET Severity=@Severity, Status=@Status, Description=@Description,
                ResolvedAt=@ResolvedAt, UpdatedAt=@UpdatedAt
            WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql, Params(entity), cancellationToken)).ConfigureAwait(false), nameof(Failure));
    }

    public override async Task DeleteAsync(Failure entity, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE dbo.Failure WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql, new { entity.Id, entity.RowVersion }, cancellationToken)).ConfigureAwait(false), nameof(Failure));
    }

    private static object Params(Failure x) => new
    {
        x.Id, x.EquipmentId, x.CauseId, Severity=(byte)x.Severity, Status=(byte)x.Status,
        x.Description, x.ReportedByPersonId, x.ReportedAt, x.ResolvedAt,
        x.CreatedAt, x.UpdatedAt, x.RowVersion,
    };
}
