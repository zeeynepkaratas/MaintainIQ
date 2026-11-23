using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class StockMovementRepository : Repository<StockMovement>, IStockMovementRepository
{
    private const string ById="""
        SELECT Id,SparePartId,WorkOrderId,MovementType,Quantity,ResultingQuantity,UnitCost,Currency,Reason,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.StockMovement WHERE Id=@Id;
        """;
    private const string All="""
        SELECT Id,SparePartId,WorkOrderId,MovementType,Quantity,ResultingQuantity,UnitCost,Currency,Reason,CreatedAt,UpdatedAt,RowVersion
        FROM dbo.StockMovement ORDER BY CreatedAt DESC;
        """;
    private readonly InternalCrudReader<StockMovement,StockMovementRow> _reader;
    public StockMovementRepository(IUnitOfWork uow):base(uow)=>_reader=new(uow,EntityMappers.ToDomain,ById,All);
    public override Task<StockMovement?> GetByIdAsync(int id,CancellationToken ct=default)=>_reader.GetByIdAsync(id,ct);
    public override Task<IReadOnlyList<StockMovement>> GetAllAsync(CancellationToken ct=default)=>_reader.GetAllAsync(ct);
    public override Task<int> AddAsync(StockMovement x,CancellationToken ct=default)
    {
        const string sql="""
            INSERT dbo.StockMovement (SparePartId,WorkOrderId,MovementType,Quantity,ResultingQuantity,UnitCost,Currency,Reason,CreatedAt,UpdatedAt)
            OUTPUT INSERTED.Id VALUES (@SparePartId,@WorkOrderId,@MovementType,@Quantity,@ResultingQuantity,@UnitCost,@Currency,@Reason,@CreatedAt,@UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql,Params(x),ct));
    }
    public override async Task UpdateAsync(StockMovement x,CancellationToken ct=default)
    {
        const string sql="""
            UPDATE dbo.StockMovement SET Reason=@Reason,UpdatedAt=@UpdatedAt WHERE Id=@Id AND RowVersion=@RowVersion;
            """;
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,Params(x),ct)).ConfigureAwait(false),nameof(StockMovement));
    }
    public override async Task DeleteAsync(StockMovement x,CancellationToken ct=default)
    {
        const string sql="DELETE dbo.StockMovement WHERE Id=@Id AND RowVersion=@RowVersion;";
        EnsureUpdated(await UnitOfWork.Connection.ExecuteAsync(Command(sql,new{x.Id,x.RowVersion},ct)).ConfigureAwait(false),nameof(StockMovement));
    }
    private static object Params(StockMovement x)=>new{x.Id,x.SparePartId,x.WorkOrderId,MovementType=(byte)x.Type,x.Quantity,x.ResultingQuantity,UnitCost=x.UnitCost.Amount,x.UnitCost.Currency,x.Reason,x.CreatedAt,x.UpdatedAt,x.RowVersion};
}
