using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class AttachmentRepository : Repository<Attachment>, IAttachmentRepository
{
    private const string ById="""
        SELECT Id,OwnerType,OwnerId,StoredFileName,OriginalFileName,ContentType,SizeBytes,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.Attachment WHERE Id=@Id;
        """;
    private const string All="""
        SELECT Id,OwnerType,OwnerId,StoredFileName,OriginalFileName,ContentType,SizeBytes,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.Attachment ORDER BY CreatedAt DESC;
        """;
    private readonly InternalCrudReader<Attachment,AttachmentRow> _reader;
    public AttachmentRepository(IUnitOfWork uow):base(uow)=>_reader=new(uow,EntityMappers.ToDomain,ById,All);
    public override Task<Attachment?> GetByIdAsync(int id,CancellationToken ct=default)=>_reader.GetByIdAsync(id,ct);
    public override Task<IReadOnlyList<Attachment>> GetAllAsync(CancellationToken ct=default)=>_reader.GetAllAsync(ct);
    public async Task<IReadOnlyList<Attachment>> GetForOwnerAsync(byte ownerType,int ownerId,CancellationToken ct=default)
    {
        const string sql="""
            SELECT Id,OwnerType,OwnerId,StoredFileName,OriginalFileName,ContentType,SizeBytes,CreatedAt,UpdatedAt,RowVersion
            FROM dbo.Attachment WHERE OwnerType=@OwnerType AND OwnerId=@OwnerId ORDER BY CreatedAt;
            """;
        var rows=await UnitOfWork.Connection.QueryAsync<AttachmentRow>(Command(sql,new{OwnerType=ownerType,OwnerId=ownerId},ct)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }
    public override Task<int> AddAsync(Attachment x,CancellationToken ct=default)
    {
        const string sql="""
            INSERT dbo.Attachment (OwnerType,OwnerId,StoredFileName,OriginalFileName,ContentType,SizeBytes,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@OwnerType,@OwnerId,@StoredFileName,@OriginalFileName,@ContentType,@SizeBytes,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql,Params(x),ct));
    }
    public override async Task UpdateAsync(Attachment x,CancellationToken ct=default)
    {
        const string sql="""
            UPDATE dbo.Attachment SET StoredFileName=@StoredFileName,OriginalFileName=@OriginalFileName,
                ContentType=@ContentType,SizeBytes=@SizeBytes,UpdatedAt=@UpdatedAt WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,Params(x),ct)).ConfigureAwait(false),nameof(Attachment));
    }
    public override async Task DeleteAsync(Attachment x,CancellationToken ct=default)
    {
        const string sql="DELETE dbo.Attachment WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,new{x.Id,x.RowVersion},ct)).ConfigureAwait(false),nameof(Attachment));
    }
    private static object Params(Attachment x)=>new{x.Id,OwnerType=(byte)x.OwnerType,x.OwnerId,x.StoredFileName,x.OriginalFileName,x.ContentType,x.SizeBytes,x.CreatedAt,x.UpdatedAt,x.RowVersion};
}
