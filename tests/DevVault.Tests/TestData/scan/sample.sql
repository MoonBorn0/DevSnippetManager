-- Aktif kullanıcıları listeler
CREATE OR ALTER PROCEDURE dbo.GetActiveUsers
AS
BEGIN
    SELECT Id, Name FROM Users WHERE IsActive = 1;
END
GO

CREATE VIEW dbo.ActiveOrders AS
SELECT Id, Total
FROM Orders
WHERE Status = 'Active';
GO
