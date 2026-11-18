SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaVersion
    (
        VersionNumber int NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
        Description nvarchar(200) NOT NULL,
        AppliedAt datetimeoffset(0) NOT NULL CONSTRAINT DF_SchemaVersion_AppliedAt DEFAULT SYSDATETIMEOFFSET()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 1)
BEGIN
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Equipment
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Equipment PRIMARY KEY,
        ParentId int NULL,
        Code nvarchar(50) NOT NULL,
        Name nvarchar(160) NOT NULL,
        Location nvarchar(200) NOT NULL,
        QrToken nvarchar(100) NULL,
        Status tinyint NOT NULL CONSTRAINT DF_Equipment_Status DEFAULT 1,
        IsDeleted bit NOT NULL CONSTRAINT DF_Equipment_IsDeleted DEFAULT 0,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Equipment_Parent FOREIGN KEY (ParentId) REFERENCES dbo.Equipment(Id),
        CONSTRAINT CK_Equipment_Status CHECK (Status BETWEEN 1 AND 4),
        CONSTRAINT CK_Equipment_NotSelfParent CHECK (ParentId IS NULL OR ParentId <> Id),
        CONSTRAINT CK_Equipment_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.FailureCause
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FailureCause PRIMARY KEY,
        Code nvarchar(40) NOT NULL,
        Name nvarchar(160) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_FailureCause_IsActive DEFAULT 1,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_FailureCause_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.Person
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Person PRIMARY KEY,
        PersonType tinyint NOT NULL,
        EmployeeNumber nvarchar(30) NOT NULL,
        FirstName nvarchar(80) NOT NULL,
        LastName nvarchar(80) NOT NULL,
        Specialty nvarchar(120) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Person_IsActive DEFAULT 1,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Person_Type CHECK (PersonType BETWEEN 1 AND 4),
        CONSTRAINT CK_Person_Specialty CHECK (PersonType = 3 OR Specialty IS NULL),
        CONSTRAINT CK_Person_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.UserAccount
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserAccount PRIMARY KEY,
        PersonId int NOT NULL,
        Username nvarchar(80) NOT NULL,
        Role tinyint NOT NULL,
        PasswordHash nvarchar(512) NOT NULL,
        PasswordSalt nvarchar(256) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_UserAccount_IsActive DEFAULT 1,
        FailedLoginCount int NOT NULL CONSTRAINT DF_UserAccount_FailedLoginCount DEFAULT 0,
        LockedUntil datetimeoffset(0) NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_UserAccount_Person FOREIGN KEY (PersonId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_UserAccount_Role CHECK (Role BETWEEN 1 AND 4),
        CONSTRAINT CK_UserAccount_FailedLoginCount CHECK (FailedLoginCount BETWEEN 0 AND 4),
        CONSTRAINT CK_UserAccount_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.Failure
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Failure PRIMARY KEY,
        EquipmentId int NOT NULL,
        CauseId int NOT NULL,
        Severity tinyint NOT NULL,
        Status tinyint NOT NULL CONSTRAINT DF_Failure_Status DEFAULT 1,
        Description nvarchar(2000) NOT NULL,
        ReportedByPersonId int NOT NULL,
        ReportedAt datetimeoffset(0) NOT NULL,
        ResolvedAt datetimeoffset(0) NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_Failure_Equipment FOREIGN KEY (EquipmentId) REFERENCES dbo.Equipment(Id),
        CONSTRAINT FK_Failure_Cause FOREIGN KEY (CauseId) REFERENCES dbo.FailureCause(Id),
        CONSTRAINT FK_Failure_ReportedBy FOREIGN KEY (ReportedByPersonId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_Failure_Severity CHECK (Severity BETWEEN 1 AND 4),
        CONSTRAINT CK_Failure_Status CHECK (Status BETWEEN 1 AND 4),
        CONSTRAINT CK_Failure_ResolvedAt CHECK (ResolvedAt IS NULL OR ResolvedAt >= ReportedAt),
        CONSTRAINT CK_Failure_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.Supplier
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Supplier PRIMARY KEY,
        Code nvarchar(40) NOT NULL,
        Name nvarchar(160) NOT NULL,
        Contact nvarchar(300) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT 1,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Supplier_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.SparePart
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SparePart PRIMARY KEY,
        SupplierId int NULL,
        Code nvarchar(50) NOT NULL,
        Name nvarchar(160) NOT NULL,
        StockQuantity decimal(18,3) NOT NULL CONSTRAINT DF_SparePart_Stock DEFAULT 0,
        MinimumStock decimal(18,3) NOT NULL CONSTRAINT DF_SparePart_MinimumStock DEFAULT 0,
        UnitCost decimal(18,2) NOT NULL CONSTRAINT DF_SparePart_UnitCost DEFAULT 0,
        Currency char(3) NOT NULL CONSTRAINT DF_SparePart_Currency DEFAULT 'TRY',
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_SparePart_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier(Id),
        CONSTRAINT CK_SparePart_Quantities CHECK (StockQuantity >= 0 AND MinimumStock >= 0),
        CONSTRAINT CK_SparePart_UnitCost CHECK (UnitCost >= 0),
        CONSTRAINT CK_SparePart_Currency CHECK (Currency = UPPER(Currency) AND Currency NOT LIKE '%[^A-Z]%'),
        CONSTRAINT CK_SparePart_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.MaintenancePlan
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MaintenancePlan PRIMARY KEY,
        EquipmentId int NOT NULL,
        Name nvarchar(160) NOT NULL,
        PeriodValue int NOT NULL,
        PeriodUnit tinyint NOT NULL,
        Priority tinyint NOT NULL,
        EstimatedDurationMinutes int NOT NULL,
        NextDueDate date NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_MaintenancePlan_IsActive DEFAULT 1,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_MaintenancePlan_Equipment FOREIGN KEY (EquipmentId) REFERENCES dbo.Equipment(Id),
        CONSTRAINT CK_MaintenancePlan_Period CHECK (PeriodValue > 0 AND PeriodUnit BETWEEN 1 AND 4),
        CONSTRAINT CK_MaintenancePlan_Priority CHECK (Priority BETWEEN 1 AND 4),
        CONSTRAINT CK_MaintenancePlan_Duration CHECK (EstimatedDurationMinutes > 0),
        CONSTRAINT CK_MaintenancePlan_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.WorkOrder
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrder PRIMARY KEY,
        WorkOrderType tinyint NOT NULL,
        Number nvarchar(50) NOT NULL,
        EquipmentId int NOT NULL,
        FailureId int NULL,
        MaintenancePlanId int NULL,
        DueDate date NULL,
        Status tinyint NOT NULL CONSTRAINT DF_WorkOrder_Status DEFAULT 1,
        Priority tinyint NOT NULL,
        Description nvarchar(2000) NOT NULL,
        PlannedStartAt datetimeoffset(0) NULL,
        PlannedEndAt datetimeoffset(0) NULL,
        AssignedTechnicianId int NULL,
        ActualStartAt datetimeoffset(0) NULL,
        CompletedAt datetimeoffset(0) NULL,
        ClosedAt datetimeoffset(0) NULL,
        LaborMinutes int NOT NULL CONSTRAINT DF_WorkOrder_LaborMinutes DEFAULT 0,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkOrder_Equipment FOREIGN KEY (EquipmentId) REFERENCES dbo.Equipment(Id),
        CONSTRAINT FK_WorkOrder_Failure FOREIGN KEY (FailureId) REFERENCES dbo.Failure(Id),
        CONSTRAINT FK_WorkOrder_MaintenancePlan FOREIGN KEY (MaintenancePlanId) REFERENCES dbo.MaintenancePlan(Id),
        CONSTRAINT FK_WorkOrder_AssignedTechnician FOREIGN KEY (AssignedTechnicianId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_WorkOrder_Type CHECK
        (
            (WorkOrderType = 1 AND FailureId IS NOT NULL AND MaintenancePlanId IS NULL AND DueDate IS NULL)
            OR
            (WorkOrderType = 2 AND FailureId IS NULL AND MaintenancePlanId IS NOT NULL AND DueDate IS NOT NULL)
        ),
        CONSTRAINT CK_WorkOrder_Status CHECK (Status BETWEEN 1 AND 7),
        CONSTRAINT CK_WorkOrder_Priority CHECK (Priority BETWEEN 1 AND 4),
        CONSTRAINT CK_WorkOrder_PlannedWindow CHECK
        (
            (PlannedStartAt IS NULL AND PlannedEndAt IS NULL)
            OR (PlannedStartAt IS NOT NULL AND PlannedEndAt > PlannedStartAt)
        ),
        CONSTRAINT CK_WorkOrder_LaborMinutes CHECK (LaborMinutes >= 0),
        CONSTRAINT CK_WorkOrder_Timeline CHECK
        (
            (ActualStartAt IS NULL OR ActualStartAt >= CreatedAt)
            AND (CompletedAt IS NULL OR (ActualStartAt IS NOT NULL AND CompletedAt >= ActualStartAt))
            AND (ClosedAt IS NULL OR (CompletedAt IS NOT NULL AND ClosedAt >= CompletedAt))
        ),
        CONSTRAINT CK_WorkOrder_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.WorkOrderPart
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderPart PRIMARY KEY,
        WorkOrderId int NOT NULL,
        SparePartId int NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        UnitCostSnapshot decimal(18,2) NOT NULL,
        Currency char(3) NOT NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkOrderPart_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder(Id),
        CONSTRAINT FK_WorkOrderPart_SparePart FOREIGN KEY (SparePartId) REFERENCES dbo.SparePart(Id),
        CONSTRAINT CK_WorkOrderPart_Values CHECK (Quantity > 0 AND UnitCostSnapshot >= 0),
        CONSTRAINT CK_WorkOrderPart_Currency CHECK (Currency = UPPER(Currency) AND Currency NOT LIKE '%[^A-Z]%'),
        CONSTRAINT CK_WorkOrderPart_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.WorkOrderStatusHistory
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderStatusHistory PRIMARY KEY,
        WorkOrderId int NOT NULL,
        FromStatus tinyint NOT NULL,
        ToStatus tinyint NOT NULL,
        ChangedByPersonId int NOT NULL,
        ChangedAt datetimeoffset(0) NOT NULL,
        Reason nvarchar(1000) NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkOrderStatusHistory_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder(Id),
        CONSTRAINT FK_WorkOrderStatusHistory_Person FOREIGN KEY (ChangedByPersonId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_WorkOrderStatusHistory_FromStatus CHECK (FromStatus BETWEEN 1 AND 7),
        CONSTRAINT CK_WorkOrderStatusHistory_ToStatus CHECK (ToStatus BETWEEN 1 AND 7),
        CONSTRAINT CK_WorkOrderStatusHistory_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.WorkOrderComment
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkOrderComment PRIMARY KEY,
        WorkOrderId int NOT NULL,
        AuthorPersonId int NOT NULL,
        Text nvarchar(2000) NOT NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_WorkOrderComment_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder(Id),
        CONSTRAINT FK_WorkOrderComment_Author FOREIGN KEY (AuthorPersonId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_WorkOrderComment_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.StockMovement
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockMovement PRIMARY KEY,
        SparePartId int NOT NULL,
        WorkOrderId int NULL,
        MovementType tinyint NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        ResultingQuantity decimal(18,3) NOT NULL,
        UnitCost decimal(18,2) NOT NULL,
        Currency char(3) NOT NULL,
        Reason nvarchar(500) NOT NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_StockMovement_SparePart FOREIGN KEY (SparePartId) REFERENCES dbo.SparePart(Id),
        CONSTRAINT FK_StockMovement_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder(Id),
        CONSTRAINT CK_StockMovement_Type CHECK (MovementType BETWEEN 1 AND 4),
        CONSTRAINT CK_StockMovement_Values CHECK (Quantity > 0 AND ResultingQuantity >= 0 AND UnitCost >= 0),
        CONSTRAINT CK_StockMovement_Currency CHECK (Currency = UPPER(Currency) AND Currency NOT LIKE '%[^A-Z]%'),
        CONSTRAINT CK_StockMovement_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.Attachment
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Attachment PRIMARY KEY,
        OwnerType tinyint NOT NULL,
        OwnerId int NOT NULL,
        StoredFileName nvarchar(260) NOT NULL,
        OriginalFileName nvarchar(260) NOT NULL,
        ContentType nvarchar(100) NOT NULL,
        SizeBytes bigint NOT NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT CK_Attachment_OwnerType CHECK (OwnerType BETWEEN 1 AND 3),
        CONSTRAINT CK_Attachment_Values CHECK (OwnerId > 0 AND SizeBytes > 0),
        CONSTRAINT CK_Attachment_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    CREATE TABLE dbo.AuditLog
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
        EntityName nvarchar(120) NOT NULL,
        EntityId nvarchar(80) NOT NULL,
        Action nvarchar(80) NOT NULL,
        PerformedByPersonId int NOT NULL,
        PerformedAt datetimeoffset(0) NOT NULL,
        OldValuesJson nvarchar(max) NULL,
        NewValuesJson nvarchar(max) NULL,
        CreatedAt datetimeoffset(0) NOT NULL,
        UpdatedAt datetimeoffset(0) NOT NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT FK_AuditLog_Person FOREIGN KEY (PerformedByPersonId) REFERENCES dbo.Person(Id),
        CONSTRAINT CK_AuditLog_OldJson CHECK (OldValuesJson IS NULL OR ISJSON(OldValuesJson) = 1),
        CONSTRAINT CK_AuditLog_NewJson CHECK (NewValuesJson IS NULL OR ISJSON(NewValuesJson) = 1),
        CONSTRAINT CK_AuditLog_Dates CHECK (UpdatedAt >= CreatedAt)
    );

    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (1, N'Initial transactional schema');

    COMMIT TRANSACTION;
END;
GO
