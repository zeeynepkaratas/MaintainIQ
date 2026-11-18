SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.SchemaVersion', N'U') IS NULL
    THROW 50001, 'V001 migration must be applied first.', 1;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersion WHERE VersionNumber = 3)
BEGIN
    BEGIN TRANSACTION;
    DECLARE @Now datetimeoffset(0) = SYSDATETIMEOFFSET();

    MERGE dbo.FailureCause AS target
    USING (VALUES
        (N'MECHANICAL', N'Mekanik arıza'),
        (N'ELECTRICAL', N'Elektrik arızası'),
        (N'ELECTRONIC', N'Elektronik arıza'),
        (N'HYDRAULIC', N'Hidrolik arıza'),
        (N'PNEUMATIC', N'Pnömatik arıza'),
        (N'OPERATOR', N'Operatör kaynaklı'),
        (N'OTHER', N'Diğer')
    ) AS source (Code, Name)
    ON target.Code = source.Code
    WHEN MATCHED THEN
        UPDATE SET Name = source.Name, IsActive = 1, UpdatedAt = @Now
    WHEN NOT MATCHED THEN
        INSERT (Code, Name, IsActive, CreatedAt, UpdatedAt)
        VALUES (source.Code, source.Name, 1, @Now, @Now);

    INSERT dbo.SchemaVersion (VersionNumber, Description)
    VALUES (3, N'Reference failure causes');
    COMMIT TRANSACTION;
END;
GO
