/*
    DB: LoadTrackerAuthDB
    Table: dbo.UserAuth

    Purpose:
    - Store CUSTOMER logins here (NOT Active Directory employees).
    - Each customer login is linked to exactly ONE customer code.
      That code must match: LoadTrackerDB.dbo.LoadTracker.[CUSTOMER]

    Permissions:
    - CanDo: 0 = ReadOnly (default)
             1 = ReadWrite
*/

USE [LoadTrackerAuthDB];
GO

IF OBJECT_ID('dbo.UserAuth', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAuth
    (
        -- Primary key (auto-generated 1,2,3...)
        UserId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_UserAuth PRIMARY KEY,

        -- Login name (unique)
        UserName NVARCHAR(100) NOT NULL
            CONSTRAINT UQ_UserAuth_UserName UNIQUE,

        -- TEMPORARY: plain text password (NOT recommended long term)
        -- Make it NOT NULL so every customer account must have a password
        [Password] NVARCHAR(200) NOT NULL,

        -- Links this login to one customer code in LoadTrackerDB.dbo.LoadTracker.[CUSTOMER]
        CustomerCode VARCHAR(10) NOT NULL,

        -- Account enabled/disabled (default = enabled)
        IsActive BIT NOT NULL
            CONSTRAINT DF_UserAuth_IsActive DEFAULT (1),

        -- Updated by your application after a successful login (store UTC)
        LastLogOnUtc DATETIME2(0) NULL,

        -- Permission flag (default = 0 read-only)
        CanDo TINYINT NOT NULL
            CONSTRAINT DF_UserAuth_CanDo DEFAULT (0),

        -- Prevent blank passwords like '' or '   '
        CONSTRAINT CK_UserAuth_Password_NotBlank
            CHECK (LEN(LTRIM(RTRIM([Password]))) > 0),

        -- Restrict CanDo to known values
        CONSTRAINT CK_UserAuth_CanDo
            CHECK (CanDo IN (0, 1))
    );
END
GO
