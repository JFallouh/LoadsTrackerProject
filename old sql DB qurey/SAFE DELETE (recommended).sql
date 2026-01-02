USE LoadTrackerDB;
GO

DELETE FROM dbo.LoadTracker
WHERE
    (
        PICK_UP_BY IS NOT NULL
        AND PICK_UP_BY < DATEADD(MONTH, -6, GETDATE())
    )
    OR
    (
        PICK_UP_BY IS NULL
        AND DELIVER_BY IS NOT NULL
        AND DELIVER_BY < DATEADD(MONTH, -6, GETDATE())
    );
GO
