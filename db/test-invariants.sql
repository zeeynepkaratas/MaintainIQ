SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @Now datetimeoffset(0) = SYSDATETIMEOFFSET();

    INSERT dbo.Equipment (Code, Name, Location, Status, CreatedAt, UpdatedAt)
    VALUES (N'TEST-ROOT', N'Test kök ekipman', N'Test alanı', 1, @Now, @Now);
    DECLARE @RootId int = SCOPE_IDENTITY();

    INSERT dbo.Equipment (ParentId, Code, Name, Location, Status, CreatedAt, UpdatedAt)
    VALUES (@RootId, N'TEST-CHILD', N'Test alt ekipman', N'Test alanı', 1, @Now, @Now);

    IF NOT EXISTS (SELECT 1 FROM dbo.vw_EquipmentHierarchy WHERE Code = N'TEST-CHILD' AND Depth = 1)
        THROW 52000, 'Recursive equipment hierarchy failed.', 1;

    BEGIN TRY
        INSERT dbo.Equipment (Code, Name, Location, Status, CreatedAt, UpdatedAt)
        VALUES (N'TEST-ROOT', N'Yinelenen kod', N'Test alanı', 1, @Now, @Now);
        THROW 52001, 'Duplicate equipment code was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 52001 THROW;
    END CATCH;

    BEGIN TRY
        INSERT dbo.SparePart (Code, Name, StockQuantity, MinimumStock, UnitCost, Currency, CreatedAt, UpdatedAt)
        VALUES (N'TEST-NEGATIVE', N'Geçersiz stok', -1, 0, 0, 'TRY', @Now, @Now);
        THROW 52002, 'Negative stock was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 52002 THROW;
    END CATCH;

    ROLLBACK TRANSACTION;
    SELECT N'PASS' AS InvariantTestResult;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
