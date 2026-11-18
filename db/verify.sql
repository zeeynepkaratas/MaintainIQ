SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @ExpectedTableCount int = 16;
DECLARE @ActualTableCount int =
(
    SELECT COUNT(*) FROM sys.tables
    WHERE schema_id = SCHEMA_ID(N'dbo')
      AND name IN (N'Equipment', N'FailureCause', N'Failure', N'WorkOrder', N'WorkOrderPart',
          N'WorkOrderStatusHistory', N'WorkOrderComment', N'SparePart', N'StockMovement', N'Supplier',
          N'Person', N'UserAccount', N'MaintenancePlan', N'Attachment', N'AuditLog', N'SchemaVersion')
);

IF @ActualTableCount <> @ExpectedTableCount THROW 51000, 'Expected database tables are missing.', 1;
IF (SELECT COUNT(*) FROM dbo.SchemaVersion) <> 3 THROW 51001, 'Not all migrations are recorded.', 1;
IF (SELECT COUNT(*) FROM dbo.FailureCause WHERE IsActive = 1) < 7 THROW 51002, 'Reference failure causes are missing.', 1;
IF OBJECT_ID(N'dbo.vw_EquipmentHierarchy', N'V') IS NULL THROW 51003, 'Equipment hierarchy view is missing.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.WorkOrder') AND name = N'RowVersion' AND system_type_id = 189)
    THROW 51004, 'WorkOrder rowversion is missing.', 1;

SELECT N'PASS' AS VerificationResult, @ActualTableCount AS TableCount,
    (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
        (SELECT object_id FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo'))) AS ForeignKeyCount,
    (SELECT COUNT(*) FROM sys.indexes WHERE object_id IN
        (SELECT object_id FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')) AND index_id > 0) AS IndexCount,
    (SELECT MAX(VersionNumber) FROM dbo.SchemaVersion) AS SchemaVersion;
GO
