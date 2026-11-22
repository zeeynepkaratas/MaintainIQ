using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class AuditRepository : Repository<AuditLog>, IAuditRepository
{
    private const string ById="""
        SELECT CONVERT(int,Id) AS Id,EntityName,EntityId,Action,PerformedByPersonId,PerformedAt,OldValuesJson,NewValuesJson,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.AuditLog WHERE Id=@Id;
        """;
    private const string All="""
        SELECT CONVERT(int,Id) AS Id,EntityName,EntityId,Action,PerformedByPersonId,PerformedAt,OldValuesJson,NewValuesJson,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.AuditLog ORDER BY PerformedAt DESC;
        """;
    private readonly InternalCrudReader<AuditLog,AuditLogRow> _reader;
    public AuditRepository(IUnitOfWork uow):base(uow)=>_reader=new(uow,EntityMappers.ToDomain,ById,All);
    public override Task<AuditLog?> GetByIdAsync(int id,CancellationToken ct=default)=>_reader.GetByIdAsync(id,ct);
    public override Task<IReadOnlyList<AuditLog>> GetAllAsync(CancellationToken ct=default)=>_reader.GetAllAsync(ct);
    public override Task<int> AddAsync(AuditLog x,CancellationToken ct=default)
    {
        const string sql="""
            INSERT dbo.AuditLog (EntityName,EntityId,Action,PerformedByPersonId,PerformedAt,OldValuesJson,NewValuesJson,CreatedAt,UpdatedAt)
            OUTPUT CONVERT(int,INSERTED.Id) VALUES (@EntityName,@EntityId,@Action,@PerformedByPersonId,@PerformedAt,@OldValuesJson,@NewValuesJson,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql,Params(x),ct));
    }
    public override async Task UpdateAsync(AuditLog x,CancellationToken ct=default)
    {
        const string sql="""
            UPDATE dbo.AuditLog SET OldValuesJson=@OldValuesJson,NewValuesJson=@NewValuesJson,UpdatedAt=@UpdatedAt WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,Params(x),ct)).ConfigureAwait(false),nameof(AuditLog));
    }
    public override async Task DeleteAsync(AuditLog x,CancellationToken ct=default)
    {
        const string sql="DELETE dbo.AuditLog WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,new{x.Id,x.RowVersion},ct)).ConfigureAwait(false),nameof(AuditLog));
    }
    private static object Params(AuditLog x)=>new{x.Id,x.EntityName,x.EntityId,x.Action,x.PerformedByPersonId,x.PerformedAt,x.OldValuesJson,x.NewValuesJson,x.CreatedAt,x.UpdatedAt,x.RowVersion};
}
