SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
    THROW 50001, 'V001 migration must be applied first.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 2)
BEGIN
    BEGIN TRANSACTION;

    CREATE UNIQUE INDEX UX_Equipment_Code ON dbo.Equipment(Code);
    CREATE UNIQUE INDEX UX_Equipment_QrToken ON dbo.Equipment(QrToken) WHERE QrToken IS NOT NULL;
    CREATE INDEX IX_Equipment_ParentId ON dbo.Equipment(ParentId) INCLUDE (Code, Name, Status, IsDeleted);
    CREATE INDEX IX_Equipment_Status ON dbo.Equipment(Status, IsDeleted);
    CREATE UNIQUE INDEX UX_FailureCause_Code ON dbo.FailureCause(Code);
    CREATE UNIQUE INDEX UX_Person_EmployeeNumber ON dbo.Person(EmployeeNumber);
    CREATE UNIQUE INDEX UX_UserAccount_PersonId ON dbo.UserAccount(PersonId);
    CREATE UNIQUE INDEX UX_UserAccount_Username ON dbo.UserAccount(Username);
    CREATE UNIQUE INDEX UX_Supplier_Code ON dbo.Supplier(Code);
    CREATE UNIQUE INDEX UX_SparePart_Code ON dbo.SparePart(Code);
    CREATE INDEX IX_Failure_Equipment_Status ON dbo.Failure(EquipmentId, Status) INCLUDE (Severity, ReportedAt);
    CREATE INDEX IX_Failure_ReportedAt ON dbo.Failure(ReportedAt DESC);
    CREATE INDEX IX_SparePart_MinimumStock ON dbo.SparePart(StockQuantity, MinimumStock) INCLUDE (Code, Name);
    CREATE INDEX IX_MaintenancePlan_Due ON dbo.MaintenancePlan(IsActive, NextDueDate) INCLUDE (EquipmentId, Priority);
    CREATE UNIQUE INDEX UX_WorkOrder_Number ON dbo.WorkOrder(Number);
    CREATE UNIQUE INDEX UX_WorkOrder_FailureId ON dbo.WorkOrder(FailureId) WHERE FailureId IS NOT NULL;
    CREATE UNIQUE INDEX UX_WorkOrder_PreventiveOccurrence ON dbo.WorkOrder(MaintenancePlanId, DueDate)
        WHERE MaintenancePlanId IS NOT NULL AND DueDate IS NOT NULL;
    CREATE INDEX IX_WorkOrder_Status_Priority ON dbo.WorkOrder(Status, Priority) INCLUDE (EquipmentId, AssignedTechnicianId);
    CREATE INDEX IX_WorkOrder_Technician_Window ON dbo.WorkOrder(AssignedTechnicianId, PlannedStartAt, PlannedEndAt)
        INCLUDE (Status) WHERE AssignedTechnicianId IS NOT NULL;
    CREATE INDEX IX_WorkOrder_Equipment ON dbo.WorkOrder(EquipmentId, Status);
    CREATE UNIQUE INDEX UX_WorkOrderPart_OrderPart ON dbo.WorkOrderPart(WorkOrderId, SparePartId);
    CREATE INDEX IX_WorkOrderStatusHistory_OrderTime ON dbo.WorkOrderStatusHistory(WorkOrderId, ChangedAt DESC);
    CREATE INDEX IX_WorkOrderComment_OrderTime ON dbo.WorkOrderComment(WorkOrderId, CreatedAt DESC);
    CREATE INDEX IX_StockMovement_PartTime ON dbo.StockMovement(SparePartId, CreatedAt DESC);
    CREATE INDEX IX_StockMovement_WorkOrder ON dbo.StockMovement(WorkOrderId) WHERE WorkOrderId IS NOT NULL;
    CREATE INDEX IX_Attachment_Owner ON dbo.Attachment(OwnerType, OwnerId);
    CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog(EntityName, EntityId, PerformedAt DESC);
    CREATE INDEX IX_AuditLog_PerformedAt ON dbo.AuditLog(PerformedAt DESC);

    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (2, N'Indexes, uniqueness rules and equipment hierarchy view');

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER VIEW dbo.vw_EquipmentHierarchy
AS
WITH EquipmentTree AS
(
    SELECT e.Id, e.ParentId, e.Code, e.Name, e.Location, e.Status, e.IsDeleted,
        CAST(0 AS int) AS Depth,
        CAST(N'/' + CONVERT(nvarchar(20), e.Id) + N'/' AS nvarchar(max)) AS HierarchyPath
    FROM dbo.Equipment AS e
    WHERE e.ParentId IS NULL

    UNION ALL

    SELECT child.Id, child.ParentId, child.Code, child.Name, child.Location, child.Status, child.IsDeleted,
        parent.Depth + 1,
        CAST(parent.HierarchyPath + CONVERT(nvarchar(20), child.Id) + N'/' AS nvarchar(max))
    FROM dbo.Equipment AS child
    INNER JOIN EquipmentTree AS parent ON parent.Id = child.ParentId
)
SELECT Id, ParentId, Code, Name, Location, Status, IsDeleted, Depth, HierarchyPath
FROM EquipmentTree;
GO
