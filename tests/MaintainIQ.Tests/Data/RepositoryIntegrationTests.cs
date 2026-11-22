using Dapper;
using MaintainIQ.Data.Common;
using MaintainIQ.Data.Connection;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;
using MaintainIQ.Domain.ValueObjects;
using Microsoft.Data.SqlClient;

namespace MaintainIQ.Tests.Data;

[Collection(DataCollection.Name)]
public sealed class RepositoryIntegrationTests
{
    private readonly SqlConnectionFactory _factory = new(DataFixture.ConnectionString);

    [Fact]
    public async Task Equipment_crud_soft_delete_and_recursive_cte_work()
    {
        await using var uow = new UnitOfWork(_factory);
        await uow.BeginAsync();
        var repository = new EquipmentRepository(uow);
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var rootId = await repository.AddAsync(new Equipment($"ROOT-{suffix}", "Tesis", "İstanbul", now));
        var root = Equipment.Rehydrate(
            rootId, null, $"ROOT-{suffix}", "Tesis", "İstanbul", null,
            EquipmentStatus.Operational, false, now, now, [1]);
        var child = new Equipment($"CHILD-{suffix}", "Hat", "İstanbul", now);
        child.MoveUnder(root);
        var childId = await repository.AddAsync(child);

        var descendants = await repository.GetDescendantsAsync(rootId);
        var path = await repository.GetPathAsync(childId);
        Assert.Contains(descendants, item => item.Id == childId);
        Assert.Equal(2, path.Count);

        var loaded = await repository.GetByIdAsync(childId);
        Assert.NotNull(loaded);
        loaded.SoftDelete(false);
        await repository.UpdateAsync(loaded);
        Assert.Null(await repository.GetByIdAsync(childId));

        await uow.RollbackAsync();
    }

    [Fact]
    public async Task Unit_of_work_commit_and_dispose_rollback_are_observable()
    {
        var committedCode = $"COMMIT-{Guid.NewGuid():N}"[..30];
        await using (var uow = new UnitOfWork(_factory))
        {
            await uow.BeginAsync();
            await uow.Connection.ExecuteAsync(
                """
                INSERT dbo.Equipment (Code,Name,Location,Status,IsDeleted,CreatedAt,UpdatedAt)
                VALUES (@Code,N'Commit test',N'Test',1,0,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
                """,
                new { Code = committedCode },
                uow.Transaction);
            await uow.CommitAsync();
        }

        var rolledBackCode = $"ROLLBACK-{Guid.NewGuid():N}"[..30];
        await using (var uow = new UnitOfWork(_factory))
        {
            await uow.BeginAsync();
            await uow.Connection.ExecuteAsync(
                """
                INSERT dbo.Equipment (Code,Name,Location,Status,IsDeleted,CreatedAt,UpdatedAt)
                VALUES (@Code,N'Rollback test',N'Test',1,0,SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
                """,
                new { Code = rolledBackCode },
                uow.Transaction);
        }

        await using var connection = new SqlConnection(DataFixture.ConnectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Equipment WHERE Code=@Code;", new { Code = committedCode }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Equipment WHERE Code=@Code;", new { Code = rolledBackCode }));
        await connection.ExecuteAsync("DELETE dbo.Equipment WHERE Code=@Code;", new { Code = committedCode });
    }

    [Fact]
    public async Task Stale_rowversion_throws_concurrency_exception()
    {
        await using var uow = new UnitOfWork(_factory);
        await uow.BeginAsync();
        var repository = new SparePartRepository(uow);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var id = await repository.AddAsync(new SparePart(
            $"PART-{suffix}", "Test parçası", 10, 2, new Money(25), DateTimeOffset.UtcNow));

        var first = await repository.GetByIdAsync(id);
        var stale = await repository.GetByIdAsync(id);
        Assert.NotNull(first);
        Assert.NotNull(stale);
        first.Adjust(9, "İlk güncelleme");
        stale.Adjust(8, "Eski sürüm");
        await repository.UpdateAsync(first);

        await Assert.ThrowsAsync<ConcurrencyException>(() => repository.UpdateAsync(stale));
        await uow.RollbackAsync();
    }

    [Fact]
    public async Task Work_order_filter_is_parameterized_and_paged()
    {
        await using var uow = new UnitOfWork(_factory);
        await uow.BeginAsync();
        var repository = new WorkOrderRepository(uow);
        var result = await repository.SearchAsync(new(
            Status: WorkOrderStatus.Created,
            Page: 1,
            PageSize: 10,
            SortBy: "CreatedAt"));
        Assert.True(result.Count <= 10);
        await uow.RollbackAsync();
    }

    [Fact]
    public async Task Get_for_update_holds_update_lock_until_transaction_ends()
    {
        var code = $"LOCK-{Guid.NewGuid():N}"[..30];
        int id;
        await using (var setup = new SqlConnection(DataFixture.ConnectionString))
        {
            await setup.OpenAsync();
            id = await setup.ExecuteScalarAsync<int>(
                """
                INSERT dbo.SparePart
                    (Code,Name,StockQuantity,MinimumStock,UnitCost,Currency,CreatedAt,UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES (@Code,N'Kilit testi',5,1,10,'TRY',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET());
                """,
                new { Code = code });
        }

        await using var uow = new UnitOfWork(_factory);
        await uow.BeginAsync();
        Assert.NotNull(await new SparePartRepository(uow).GetForUpdateAsync(id));

        await using var competitor = new SqlConnection(DataFixture.ConnectionString);
        await competitor.OpenAsync();
        var exception = await Assert.ThrowsAsync<SqlException>(() => competitor.ExecuteAsync(
            "SET LOCK_TIMEOUT 500; UPDATE dbo.SparePart SET StockQuantity=4 WHERE Id=@Id;",
            new { Id = id }));
        Assert.Equal(1222, exception.Number);

        await uow.RollbackAsync();
        await competitor.ExecuteAsync("DELETE dbo.SparePart WHERE Id=@Id;", new { Id = id });
    }
}
