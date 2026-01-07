USE [LoadTrackerAuthDB];
GO

-- Option A usernames: acronym + last 4 digits of customer id

-- MERCURY MARINE LTD. (CustomerId = 18396) => mm-8396
INSERT INTO dbo.UserAuth (UserName, [Password], CustomerCode)
VALUES ('mm-8396', 'Temp#18396!', '18396');

-- NATIONAL LOGISTICS SERVICES (2006) INC. (CustomerId = 45289) => nls-5289
INSERT INTO dbo.UserAuth (UserName, [Password], CustomerCode)
VALUES ('nls-5289', 'Temp#45289!', '45289');
GO
