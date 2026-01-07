/*
    LoadTrackerAuthDB - Customer login table (SQL Server 2016)

    Purpose:
    - Store CUSTOMER accounts here (not Active Directory employees).
    - Minimal fields: username, password (temporary), is_active, last_log_on, can_do.

    Important:
    - Storing plain-text passwords is unsafe. You asked "for now", so this is temporary.
      Later replace with PasswordHash + PasswordSalt.
*/

USE [LoadTrackerAuthDB];
GO

IF OBJECT_ID('dbo.UserAuth', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAuth
    (
        -- Primary key (auto-increment)
        UserId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_UserAuth PRIMARY KEY,

        -- Customer login username (must be unique)
        UserName NVARCHAR(100) NOT NULL
            CONSTRAINT UQ_UserAuth_UserName UNIQUE,

        -- TEMPORARY: plain text password (replace later with hash/salt)
        [Password] NVARCHAR(200) NULL,

        -- 1 = active, 0 = disabled (default active)
        IsActive BIT NOT NULL
            CONSTRAINT DF_UserAuth_IsActive DEFAULT (1),

        -- Last successful login time (store UTC)
        LastLogOnUtc DATETIME2(0) NULL,

        -- Permission: 0 = ReadOnly (default), 1 = ReadWrite
        CanDo TINYINT NOT NULL
            CONSTRAINT DF_UserAuth_CanDo DEFAULT (0),

        -- Row creation time (UTC)
        CreatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_UserAuth_CreatedUtc DEFAULT (SYSUTCDATETIME()),

        -- Last update time (UTC) - your app updates this on edits
        UpdatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_UserAuth_UpdatedUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
