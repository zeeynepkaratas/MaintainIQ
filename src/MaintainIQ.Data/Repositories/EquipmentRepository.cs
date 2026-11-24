using Dapper;
using MaintainIQ.Data.Persistence.Mappers;
using MaintainIQ.Data.Persistence.Rows;
using MaintainIQ.Data.ReadModels;
using MaintainIQ.Data.Transactions;
using MaintainIQ.Domain.Entities;

namespace MaintainIQ.Data.Repositories;

public sealed class EquipmentRepository(IUnitOfWork unitOfWork)
    : Repository<Equipment>(unitOfWork), IEquipmentRepository
{
    public override async Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ParentId, Code, Name, Location, QrToken, Status, IsDeleted, IsCritical, WarrantyEndDate, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.Equipment
            WHERE Id = @Id AND IsDeleted = 0;
            """;
        var row = await UnitOfWork.Connection.QuerySingleOrDefaultAsync<EquipmentRow>(
            Command(sql, new { Id = id }, cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public override async Task<IReadOnlyList<Equipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ParentId, Code, Name, Location, QrToken, Status, IsDeleted, IsCritical, WarrantyEndDate, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.Equipment WHERE IsDeleted = 0 ORDER BY Code;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<EquipmentRow>(
            Command(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public override async Task<int> AddAsync(Equipment entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.Equipment (ParentId, Code, Name, Location, QrToken, Status, IsDeleted, IsCritical, WarrantyEndDate, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@ParentId, @Code, @Name, @Location, @QrToken, @Status, @IsDeleted, @IsCritical, @WarrantyEndDate, @CreatedAt, @UpdatedAt);
            """;
        return await UnitOfWork.Connection.ExecuteScalarAsync<int>(Command(sql, new
        {
            entity.ParentId,
            entity.Code,
            entity.Name,
            entity.Location,
            entity.QrToken,
            Status = (byte)entity.Status,
            entity.IsDeleted,
            entity.IsCritical,
            WarrantyEndDate = ToDate(entity.WarrantyEndDate),
            entity.CreatedAt,
            entity.UpdatedAt,
        }, cancellationToken)).ConfigureAwait(false);
    }

    public override async Task UpdateAsync(Equipment entity, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.Equipment
            SET ParentId = @ParentId, Code = @Code, Name = @Name, Location = @Location,
                QrToken = @QrToken, Status = @Status, IsDeleted = @IsDeleted, IsCritical = @IsCritical,
                WarrantyEndDate = @WarrantyEndDate, UpdatedAt = @UpdatedAt
            WHERE Id = @Id AND RowVersion = @RowVersion;
            """;
        var affected = await UnitOfWork.Connection.ExecuteAsync(Command(sql, new
        {
            entity.Id,
            entity.ParentId,
            entity.Code,
            entity.Name,
            entity.Location,
            entity.QrToken,
            Status = (byte)entity.Status,
            entity.IsDeleted,
            entity.IsCritical,
            WarrantyEndDate = ToDate(entity.WarrantyEndDate),
            entity.UpdatedAt,
            entity.RowVersion,
        }, cancellationToken)).ConfigureAwait(false);
        EnsureUpdated(affected, nameof(Equipment));
    }

    public override Task DeleteAsync(Equipment entity, CancellationToken cancellationToken = default) =>
        UpdateAsync(entity, cancellationToken);

    public async Task<IReadOnlyList<EquipmentTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH EquipmentTree AS
            (
                SELECT Id, ParentId, Code, Name, Location, Status, 0 AS Level,
                       CAST(Code AS nvarchar(max)) AS Path
                FROM dbo.Equipment WHERE ParentId IS NULL AND IsDeleted = 0
                UNION ALL
                SELECT e.Id, e.ParentId, e.Code, e.Name, e.Location, e.Status, t.Level + 1,
                       CAST(t.Path + N' > ' + e.Code AS nvarchar(max))
                FROM dbo.Equipment e
                INNER JOIN EquipmentTree t ON e.ParentId = t.Id
                WHERE e.IsDeleted = 0
            )
            SELECT Id, ParentId, Code, Name, Location, Status, Level, Path
            FROM EquipmentTree ORDER BY Path OPTION (MAXRECURSION 100);
            """;
        var items = await UnitOfWork.Connection.QueryAsync<EquipmentTreeItemDto>(
            Command(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return items.ToArray();
    }

    public async Task<IReadOnlyList<EquipmentTreeItemDto>> GetDescendantsAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH Descendants AS
            (
                SELECT Id, ParentId, Code, Name, Location, Status, 0 AS Level,
                       CAST(Code AS nvarchar(max)) AS Path
                FROM dbo.Equipment WHERE Id = @Id AND IsDeleted = 0
                UNION ALL
                SELECT e.Id, e.ParentId, e.Code, e.Name, e.Location, e.Status, d.Level + 1,
                       CAST(d.Path + N' > ' + e.Code AS nvarchar(max))
                FROM dbo.Equipment e INNER JOIN Descendants d ON e.ParentId = d.Id
                WHERE e.IsDeleted = 0
            )
            SELECT Id, ParentId, Code, Name, Location, Status, Level, Path
            FROM Descendants WHERE Id <> @Id ORDER BY Path OPTION (MAXRECURSION 100);
            """;
        var items = await UnitOfWork.Connection.QueryAsync<EquipmentTreeItemDto>(
            Command(sql, new { Id = id }, cancellationToken)).ConfigureAwait(false);
        return items.ToArray();
    }

    public async Task<IReadOnlyList<EquipmentTreeItemDto>> GetPathAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH Ancestors AS
            (
                SELECT Id, ParentId, Code, Name, Location, Status, 0 AS Level,
                       CAST(Code AS nvarchar(max)) AS Path
                FROM dbo.Equipment WHERE Id = @Id AND IsDeleted = 0
                UNION ALL
                SELECT e.Id, e.ParentId, e.Code, e.Name, e.Location, e.Status, a.Level + 1,
                       CAST(e.Code + N' > ' + a.Path AS nvarchar(max))
                FROM dbo.Equipment e INNER JOIN Ancestors a ON e.Id = a.ParentId
                WHERE e.IsDeleted = 0
            )
            SELECT Id, ParentId, Code, Name, Location, Status, Level, Path
            FROM Ancestors ORDER BY Level DESC OPTION (MAXRECURSION 100);
            """;
        var items = await UnitOfWork.Connection.QueryAsync<EquipmentTreeItemDto>(
            Command(sql, new { Id = id }, cancellationToken)).ConfigureAwait(false);
        return items.ToArray();
    }

    public async Task<IReadOnlyList<Equipment>> SearchAsync(string filter, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ParentId, Code, Name, Location, QrToken, Status, IsDeleted, IsCritical, WarrantyEndDate, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.Equipment
            WHERE IsDeleted = 0 AND (Code LIKE @Filter OR Name LIKE @Filter OR Location LIKE @Filter)
            ORDER BY Code;
            """;
        var rows = await UnitOfWork.Connection.QueryAsync<EquipmentRow>(
            Command(sql, new { Filter = $"%{filter.Trim()}%" }, cancellationToken)).ConfigureAwait(false);
        return rows.Select(EntityMappers.ToDomain).ToArray();
    }

    public Task<bool> ExistsOpenWorkAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS
            (
                SELECT 1 FROM dbo.WorkOrder WHERE EquipmentId = @Id AND Status NOT IN (6, 7)
            ) THEN 1 ELSE 0 END AS bit);
            """;
        return UnitOfWork.Connection.ExecuteScalarAsync<bool>(Command(sql, new { Id = id }, cancellationToken));
    }

    public async Task<Equipment?> GetByQrTokenAsync(string qrToken, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, ParentId, Code, Name, Location, QrToken, Status, IsDeleted, IsCritical, WarrantyEndDate, CreatedAt, UpdatedAt, RowVersion
            FROM dbo.Equipment
            WHERE QrToken = @QrToken AND IsDeleted = 0;
            """;
        var row = await UnitOfWork.Connection.QuerySingleOrDefaultAsync<EquipmentRow>(
            Command(sql, new { QrToken = qrToken }, cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    private static DateTime? ToDate(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);
}
