-- Schema for shop
CREATE TABLE customers (
    id INT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    note VARCHAR(200) DEFAULT 'it''s ok; fine'
);
GO

-- Gets orders for a customer
CREATE PROCEDURE dbo.GetOrders
    @CustomerId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM orders WHERE customer_id = @CustomerId; -- GO inside comment
END
GO

/* Adds numbers
   GO
*/
CREATE OR ALTER FUNCTION dbo.AddNumbers(@a INT, @b INT)
RETURNS INT
AS
BEGIN
    RETURN @a + @b;
END;
GO

CREATE VIEW v_active AS
SELECT id, name
FROM customers
WHERE name <> '';
GO

CREATE INDEX ix_customers_name ON customers(name);
INSERT INTO customers VALUES (1, 'a', 'b');

CREATE OR REPLACE FUNCTION pg_add(a int, b int) RETURNS int AS $$
BEGIN
    RETURN a + b;
END;
$$ LANGUAGE plpgsql;

CREATE TABLE IF NOT EXISTS audit (
    id INT,
    at TIMESTAMP,
    what TEXT
);

CREATE TRIGGER trg_audit AFTER INSERT ON customers
FOR EACH ROW
BEGIN
    INSERT INTO audit VALUES (NEW.id, NOW(), 'x');
END;
GO

DROP TABLE tmp;
SELECT 1;
