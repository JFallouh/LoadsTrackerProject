USE LoadTrackerDB;
GO

CREATE TABLE dbo.LoadTracker (
    DETAIL_LINE_ID        INT            NOT NULL,
    BILL_NUMBER           VARCHAR(20)     NULL,
    [BOL #]               VARCHAR(40)     NULL,
    [ORDER #]             VARCHAR(40)     NULL,

    DESTINATION           VARCHAR(10)     NULL,
    DESTNAME              VARCHAR(40)     NULL,
    DESTCITY              VARCHAR(30)     NULL,
    DESTPROV              VARCHAR(4)      NULL,

    CUSTOMER               VARCHAR(10)     NULL,
    CALLNAME               VARCHAR(40)     NULL,

    ORIGIN                 VARCHAR(10)     NULL,
    ORIGNAME               VARCHAR(40)     NULL,
    ORIGCITY               VARCHAR(30)     NULL,
    ORIGPROV               VARCHAR(4)      NULL,

    PICK_UP_BY             DATETIME       NULL,
    PICK_UP_BY_END         DATETIME       NULL,
    DELIVER_BY             DATETIME       NULL,
    DELIVER_BY_END         DATETIME       NULL,

    CURRENT_STATUS         VARCHAR(10)     NULL,
    PALLETS                FLOAT           NULL,
    CUBE                   FLOAT           NULL,
    WEIGHT                 FLOAT           NULL,
    CUBE_UNITS             VARCHAR(3)      NULL,
    WEIGHT_UNITS           VARCHAR(3)      NULL,
    TEMPERATURE            FLOAT           NULL,
    TEMPERATURE_UNITS      VARCHAR(5)      NULL,

    DANGEROUS_GOODS        CHAR(5)          NULL,
    REQUESTED_EQUIPMEN     VARCHAR(20)     NULL
);
GO



USE LoadTrackerDB;
GO

ALTER TABLE dbo.LoadTracker
ADD ACTUAL_DELIVERY DATETIME NULL;
GO


USE LoadTrackerDB;
GO

ALTER TABLE dbo.LoadTracker
ADD
   
    COMMENTS           VARCHAR(300) NULL;
GO

USE LoadTrackerDB;
GO

IF COL_LENGTH('dbo.LoadTracker', 'COMMENTS') IS NULL
BEGIN
    ALTER TABLE dbo.LoadTracker
    ADD
   
    COMMENTS           VARCHAR(300) NULL;
END
GO


USE LoadTrackerDB;
GO
IF COL_LENGTH('dbo.LoadTracker', 'SF_SHORT_DESC') IS NULL
BEGIN
    ALTER TABLE dbo.LoadTracker
    ADD [SF_SHORT_DESC] VARCHAR(2000) NULL;
END
GO



USE LoadTrackerDB;
GO

ALTER TABLE dbo.LoadTracker
ADD [PO #"] VARCHAR(40)     NULL,;
GO

              