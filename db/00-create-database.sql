IF DB_ID(N'MaintainIQ') IS NULL
BEGIN
    CREATE DATABASE [MaintainIQ];
END;
GO

SELECT [name], [state_desc]
FROM sys.databases
WHERE [name] = N'MaintainIQ';
GO
