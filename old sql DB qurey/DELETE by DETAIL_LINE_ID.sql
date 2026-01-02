--Preview what will be deleted

SELECT *
FROM dbo.LoadTracker
WHERE DETAIL_LINE_ID BETWEEN 26594 AND 29153;




USE LoadTrackerDB;
GO

DELETE FROM dbo.LoadTracker
WHERE DETAIL_LINE_ID BETWEEN 26594 AND 29153;
GO








--never tried this before 
--Ultra-safe 
BEGIN TRAN;

DELETE FROM dbo.LoadTracker
WHERE DETAIL_LINE_ID BETWEEN 26594 AND 29153;

SELECT @@ROWCOUNT AS RowsDeleted;

-- If correct:
COMMIT TRAN;

-- If not:
-- ROLLBACK TRAN;
