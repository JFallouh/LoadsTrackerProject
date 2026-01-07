USE [LoadTrackerAuthDB];
GO

IF OBJECT_ID('dbo.UserAuth', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAuth
    (
        UserId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_UserAuth PRIMARY KEY,

        UserName NVARCHAR(100) NOT NULL
            CONSTRAINT UQ_UserAuth_UserName UNIQUE,

        [Password] NVARCHAR(200) NULL,

        IsActive BIT NOT NULL
            CONSTRAINT DF_UserAuth_IsActive DEFAULT (1),

        LastLogOnUtc DATETIME2(0) NULL,

        CanDo TINYINT NOT NULL
            CONSTRAINT DF_UserAuth_CanDo DEFAULT (0)
    );
END
GO
