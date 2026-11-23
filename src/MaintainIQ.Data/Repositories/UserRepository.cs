using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class UserRepository : Repository<UserAccount>, IUserRepository
{
    private const string ById="""
        SELECT Id,PersonId,Username,Role,PasswordHash,PasswordSalt,IsActive,FailedLoginCount,LockedUntil,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.UserAccount WHERE Id=@Id;
        """;
    private const string All="""
        SELECT Id,PersonId,Username,Role,PasswordHash,PasswordSalt,IsActive,FailedLoginCount,LockedUntil,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.UserAccount ORDER BY Username;
        """;
    private readonly InternalCrudReader<UserAccount,UserAccountRow> _reader;
    public UserRepository(IUnitOfWork uow):base(uow)=>_reader=new(uow,EntityMappers.ToDomain,ById,All);
    public override Task<UserAccount?> GetByIdAsync(int id,CancellationToken ct=default)=>_reader.GetByIdAsync(id,ct);
    public override Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken ct=default)=>_reader.GetAllAsync(ct);
    public async Task<UserAccount?> GetByUsernameAsync(string username,CancellationToken ct=default)
    {
        const string sql="""
            SELECT Id,PersonId,Username,Role,PasswordHash,PasswordSalt,IsActive,FailedLoginCount,LockedUntil,CreatedAt,UpdatedAt,RowVersion
            FROM dbo.UserAccount WHERE Username=@Username;
            """;
        var row=await UnitOfWork.Connection.QuerySingleOrDefaultAsync<UserAccountRow>(Command(sql,new{Username=username.Trim().ToLowerInvariant()},ct)).ConfigureAwait(false);
        return row?.ToDomain();
    }
    public override Task<int> AddAsync(UserAccount x,CancellationToken ct=default)
    {
        const string sql="""
            INSERT dbo.UserAccount (PersonId,Username,Role,PasswordHash,PasswordSalt,IsActive,FailedLoginCount,LockedUntil,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@PersonId,@Username,@Role,@PasswordHash,@PasswordSalt,@IsActive,@FailedLoginCount,@LockedUntil,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql,Params(x),ct));
    }
    public override async Task UpdateAsync(UserAccount x,CancellationToken ct=default)
    {
        const string sql="""
            UPDATE dbo.UserAccount SET Username=@Username,Role=@Role,PasswordHash=@PasswordHash,PasswordSalt=@PasswordSalt,
                IsActive=@IsActive,FailedLoginCount=@FailedLoginCount,LockedUntil=@LockedUntil,UpdatedAt=@UpdatedAt
            WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,Params(x),ct)).ConfigureAwait(false),nameof(UserAccount));
    }
    public override async Task DeleteAsync(UserAccount x,CancellationToken ct=default)
    {
        const string sql="UPDATE dbo.UserAccount SET IsActive=0,UpdatedAt=@UpdatedAt WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,new{x.Id,x.UpdatedAt,x.RowVersion},ct)).ConfigureAwait(false),nameof(UserAccount));
    }
    private static object Params(UserAccount x)=>new{x.Id,x.PersonId,x.Username,Role=(byte)x.Role,x.PasswordHash,x.PasswordSalt,x.IsActive,x.FailedLoginCount,x.LockedUntil,x.CreatedAt,x.UpdatedAt,x.RowVersion};
}
