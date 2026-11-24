SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
    THROW 50001, 'V001 migration must be applied first.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 4)
BEGIN
    BEGIN TRANSACTION;

    -- Advisor R6/R7 için ekipman kritikliği ve garanti bitişi.
    IF COL_LENGTH(N'dbo.Equipment', N'IsCritical') IS NULL
        ALTER TABLE dbo.Equipment ADD
            IsCritical bit NOT NULL CONSTRAINT DF_Equipment_IsCritical DEFAULT 0,
            WarrantyEndDate date NULL;

    -- Plan motoru: takvim kaymasını önleyen çapa tarihi ve son tamamlanma zamanı.
    IF COL_LENGTH(N'dbo.MaintenancePlan', N'AnchorDate') IS NULL
        ALTER TABLE dbo.MaintenancePlan ADD
            AnchorDate date NULL,
            LastDoneAt datetimeoffset(0) NULL;

    COMMIT TRANSACTION;
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 4)
BEGIN
    BEGIN TRANSACTION;

    UPDATE dbo.MaintenancePlan SET AnchorDate = NextDueDate WHERE AnchorDate IS NULL;
    ALTER TABLE dbo.MaintenancePlan ALTER COLUMN AnchorDate date NOT NULL;

    -- KPI ve Advisor sorguları: dönem + ekipman filtreli arıza ve iş emri taramaları.
    -- Gerekçe ve yürütme planı notu: docs/kpi.md.
    CREATE INDEX IX_Failure_Equipment_ReportedAt ON dbo.Failure(EquipmentId, ReportedAt)
        INCLUDE (CauseId, Severity, Status, ResolvedAt);
    CREATE INDEX IX_WorkOrder_Equipment_CreatedAt ON dbo.WorkOrder(EquipmentId, CreatedAt)
        INCLUDE (WorkOrderType, Status, FailureId, DueDate, ActualStartAt, CompletedAt, ClosedAt,
                 LaborMinutes, AssignedTechnicianId);

    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (4, N'Maintenance engine, equipment criticality and analytics indexes');

    COMMIT TRANSACTION;
END;
GO
