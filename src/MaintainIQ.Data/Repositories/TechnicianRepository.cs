using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class TechnicianRepository : Repository<Technician>, ITechnicianRepository
{
    private const string ById = """
        SELECT Id,EmployeeNumber,FirstName,LastName,Specialty,IsActive,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.Person WHERE Id=@Id AND PersonType=3;
        """;
    private const string All = """
        SELECT Id,EmployeeNumber,FirstName,LastName,Specialty,IsActive,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.Person WHERE PersonType=3 ORDER BY FirstName,LastName;
        """;
    private readonly InternalCrudReader<Technician, TechnicianRow> _reader;
    public TechnicianRepository(IUnitOfWork unitOfWork) : base(unitOfWork) => _reader = new(unitOfWork, EntityMappers.ToDomain, ById, All);
    public override Task<Technician?> GetByIdAsync(int id, CancellationToken cancellationToken=default)=>_reader.GetByIdAsync(id,cancellationToken);
    public override Task<IReadOnlyList<Technician>> GetAllAsync(CancellationToken cancellationToken=default)=>_reader.GetAllAsync(cancellationToken);
    public override Task<int> AddAsync(Technician x, CancellationToken cancellationToken=default)
    {
        const string sql="""
            INSERT dbo.Person (PersonType,EmployeeNumber,FirstName,LastName,Specialty,IsActive,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (3,@EmployeeNumber,@FirstName,@LastName,@Specialty,@IsActive,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql,Params(x),cancellationToken));
    }
    public override async Task UpdateAsync(Technician x,CancellationToken cancellationToken=default)
    {
        const string sql="""
            UPDATE dbo.Person SET EmployeeNumber=@EmployeeNumber,FirstName=@FirstName,LastName=@LastName,
                Specialty=@Specialty,IsActive=@IsActive,UpdatedAt=@UpdatedAt
            WHERE Id=@Id AND PersonType=3 AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,Params(x),cancellationToken)).ConfigureAwait(false),nameof(Technician));
    }
    public override async Task DeleteAsync(Technician x,CancellationToken cancellationToken=default)
    {
        const string sql="UPDATE dbo.Person SET IsActive=0,UpdatedAt=@UpdatedAt WHERE Id=@Id AND PersonType=3 AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,new{x.Id,x.UpdatedAt,x.RowVersion},cancellationToken)).ConfigureAwait(false),nameof(Technician));
    }
    private static object Params(Technician x)=>new{x.Id,x.EmployeeNumber,x.FirstName,x.LastName,x.Specialty,x.IsActive,x.CreatedAt,x.UpdatedAt,x.RowVersion};
}
