/* 
    LoadTrackerAuthDB - Customer login table (SQL Server 2016)

    Goal:
    - Store CUSTOMER accounts here (NOT Active Directory employees).
    - Keep it simple: username, password (for now), is_active, last_log_on, can_do (permission).

    Notes:
    - Plain-text passwords are NOT recommended. This script uses a plain-text column
      only because you said “for now”. Later we should replace it with PasswordHash + Salt.
*/

USE [LoadTrackerAuthDB];
GO

/* 
    Drop table (ONLY if you want to recreate from scratch).
    Keep commented to avoid accidental loss.
*/
--IF OBJECT_ID('dbo.UserAuth', 'U') IS NOT NULL
--    DROP TABLE dbo.UserAuth;
--GO

/* 
    Create table if it doesn't exist
*/
IF OBJECT_ID('dbo.UserAuth', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAuth
    (
        /* 
            Primary key.
            IDENTITY(1,1) auto-generates 1,2,3...
        */
        UserId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_UserAuth PRIMARY KEY,

        /*
            Login username for the customer.
            NVARCHAR supports Unicode (safer for names).
            Unique constraint prevents duplicates.
        */
        UserName NVARCHAR(100) NOT NULL
            CONSTRAINT UQ_UserAuth_UserName UNIQUE,

        /*
            For now: plain-text password (NOT recommended).
            Later: replace with PasswordHash VARBINARY + PasswordSalt VARBINARY.
        */
        [Password] NVARCHAR(200) NULL,

        /*
            Enables/disables the account.
            Default = 1 (active).
        */
        IsActive BIT NOT NULL
            CONSTRAINT DF_UserAuth_IsActive DEFAULT (1),

        /*
            When the user last logged in (store UTC).
            Your app updates this after successful login.
        */
        LastLogOnUtc DATETIME2(0) NULL,

        /*
            Permission / capability:
            0 = ReadOnly (default)
            1 = ReadWrite
            (You can add more later if needed.)
        */
        CanDo TINYINT NOT NULL
            CONSTRAINT DF_UserAuth_CanDo DEFAULT (0),

        /*
            Audit columns: when row was created / last updated.
            These default values run only on INSERT.
            Your app should update UpdatedUtc on changes.
        */
        CreatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_UserAuth_CreatedUtc DEFAULT (SYSUTCDATETIME()),

        UpdatedUtc DATETIME2(0) NOT NULL
            CONSTRAINT DF_UserAuth_UpdatedUtc DEFAULT (SYSUTCDATETIME())
    );

    /*
        Helpful index: speeds up queries like:
        WHERE IsActive = 1 AND UserName = @UserName
    */
    CREATE INDEX IX_UserAuth_IsActive_UserName
        ON dbo.UserAuth (IsActive, UserName);
END
GO
