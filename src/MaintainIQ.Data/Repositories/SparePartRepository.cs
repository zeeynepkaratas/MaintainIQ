using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class SparePartRepository(IUnitOfWork unitOfWork)
    : Repository<SparePart>(unitOfWork), ISparePartRepository
{
    public override async Task<SparePart?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, SupplierId, Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.SparePart WHERE Id = @Id;
            """;
        var row = await QuerySingleAsync(sql, id, cancellationToken).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public async Task<SparePart?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, SupplierId, Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.SparePart WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;
            """;
        var row = await QuerySingleAsync(sql, id, cancellationToken).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public override async Task<IReadOnlyList<SparePart>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, SupplierId, Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.SparePart ORDER BY Code;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<SparePartRow>(Command(
            sql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<SparePart>> GetBelowMinimumAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, SupplierId, Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.SparePart WHERE StockQuantity < MinimumStock ORDER BY Code;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<SparePartRow>(Command(
            sql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public override Task<int> AddAsync(SparePart entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.SparePart
                (SupplierId, Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@SupplierId, @Code, @Name, @StockQuantity, @MinimumStock, @UnitCost, @Currency, @CreatedAt, @UpdatedAt);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql, Parameters(entity), cancellationToken));
    }

    public override async Task UpdateAsync(SparePart entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.SparePart
            SET SupplierId = @SupplierId, Code = @Code, Name = @Name,
                StockQuantity = @StockQuantity, MinimumStock = @MinimumStock,
                UnitCost = @UnitCost, Currency = @Currency, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND RowVersion = @RowVersion;
            """;
        var affected = await UnitOfWork.Connection.ExecuteAsync(
            Command(sql, Parameters(entity), cancellationToken)).ConfigureAwait(false);
        EnsureUpdated(affected, nameof(SparePart));
    }

    public override async Task DeleteAsync(SparePart entity, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE dbo.SparePart WHERE Id = @Id AND RowVersion = @RowVersion;";
        var affected = await UnitOfWork.Connection.ExecuteAsync(
            Command(sql, new { entity.Id, entity.RowVersion }, cancellationToken)).ConfigureAwait(false);
        EnsureUpdated(affected, nameof(SparePart));
    }

    private Task<SparePartRow?> QuerySingleAsync(string sql, int id, CancellationToken cancellationToken) =>
        UnitOfWork.Connection.QuerySingleOrDefaultAsync<SparePartRow>(
            Command(sql, new { Id = id }, cancellationToken));

    private static object Parameters(SparePart entity) => new
    {
        entity.Id,
        entity.SupplierId,
        entity.Code,
        entity.Name,
        entity.StockQuantity,
        entity.MinimumStock,
        UnitCost = entity.UnitCost.Amount,
        entity.UnitCost.Currency,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.RowVersion,
    };
}
