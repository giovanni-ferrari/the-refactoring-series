IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'OrderService')
BEGIN
    CREATE DATABASE OrderService;
END
GO

USE OrderService;

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Order')
BEGIN
    EXEC('CREATE SCHEMA [Order]'); 
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders' AND schema_id = SCHEMA_ID('Order'))
BEGIN
    CREATE TABLE [Order].Orders (
        Id INT PRIMARY KEY IDENTITY(1,1),
        OrderNumber NVARCHAR(50) NOT NULL,
        OrderDate DATETIME NOT NULL,
        CustomerName NVARCHAR(100) NOT NULL,
        CustomerAddress NVARCHAR(200) NOT NULL,
        CustomerEmail NVARCHAR(100) NOT NULL,
        CustomerPhone NVARCHAR(20) NOT NULL,
        Status INT NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItems' AND schema_id = SCHEMA_ID('Order'))
BEGIN
    CREATE TABLE [Order].OrderItems (
        Id INT PRIMARY KEY IDENTITY(1,1),
        OrderId INT NOT NULL,
        ProductCode NVARCHAR(50) NOT NULL,
        ProductName NVARCHAR(100) NOT NULL,
        Quantity INT NOT NULL,
        FOREIGN KEY (OrderId) REFERENCES [Order].Orders(Id)
    );
END
GO