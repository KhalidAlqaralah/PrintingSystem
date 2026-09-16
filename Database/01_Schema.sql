IF DB_ID('Everest') IS NULL
    CREATE DATABASE Everest;
GO
USE Everest;
GO

CREATE TABLE Entities (
    EntityId      INT IDENTITY(1,1) PRIMARY KEY,
    EntityType    TINYINT       NOT NULL,          -- 1 = Doctor, 2 = Hospital
    Name          NVARCHAR(200) NOT NULL,
    LicenseNumber NVARCHAR(50)  NOT NULL,          -- Union ID / National number
    Major         NVARCHAR(150) NULL,
    Address       NVARCHAR(300) NULL,
    LastSequence  INT           NOT NULL CONSTRAINT DF_Entities_Seq     DEFAULT 0,
    IsActive      BIT           NOT NULL CONSTRAINT DF_Entities_Active  DEFAULT 1,
    CreatedAt     DATETIME2     NOT NULL CONSTRAINT DF_Entities_Created DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Entities_Type    CHECK (EntityType IN (1,2)),
    CONSTRAINT UQ_Entities_License UNIQUE (LicenseNumber)
);
GO

CREATE TABLE EntityPhones (
    PhoneId     INT IDENTITY(1,1) PRIMARY KEY,
    EntityId    INT          NOT NULL,
    PhoneType   TINYINT      NOT NULL,             -- 1 = Doctor/Mobile, 2 = Clinic/Hospital
    PhoneNumber NVARCHAR(30) NOT NULL,
    SlotIndex   TINYINT      NOT NULL,             -- 1 or 2
    CONSTRAINT FK_Phones_Entities FOREIGN KEY (EntityId) REFERENCES Entities(EntityId) ON DELETE CASCADE,
    CONSTRAINT CK_Phones_Type CHECK (PhoneType IN (1,2)),
    CONSTRAINT CK_Phones_Slot CHECK (SlotIndex IN (1,2)),
    CONSTRAINT UQ_Phones      UNIQUE (EntityId, PhoneType, SlotIndex)
);
GO

CREATE TABLE DeliveryNotes (
    DeliveryNoteId INT IDENTITY(1000,1) PRIMARY KEY,
    NoteDate       DATE          NOT NULL CONSTRAINT DF_Notes_Date    DEFAULT CAST(SYSDATETIME() AS DATE),
    RecipientName  NVARCHAR(200) NULL,
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_Notes_Created DEFAULT SYSDATETIME()
);
GO

CREATE TABLE Orders (
    OrderId                 INT IDENTITY(1,1) PRIMARY KEY,
    EntityId                INT     NOT NULL,
    NotebookCount           INT     NOT NULL,
    AppointmentsPerNotebook INT     NOT NULL CONSTRAINT DF_Orders_PerNb DEFAULT 3,
    StartSerial             CHAR(8) NOT NULL,
    EndSerial               CHAR(8) NOT NULL,
    TotalAppointments       AS (NotebookCount * AppointmentsPerNotebook) PERSISTED,
    DeliveryNoteId          INT       NULL,
    PrintedAt               DATETIME2 NULL,
    LastPrintedSerial       CHAR(8)   NULL,
    CreatedAt               DATETIME2 NOT NULL CONSTRAINT DF_Orders_Created DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Orders_Entities FOREIGN KEY (EntityId)       REFERENCES Entities(EntityId),
    CONSTRAINT FK_Orders_Notes    FOREIGN KEY (DeliveryNoteId) REFERENCES DeliveryNotes(DeliveryNoteId),
    CONSTRAINT CK_Orders_Count    CHECK (NotebookCount > 0)
);
GO
CREATE INDEX IX_Orders_Entity  ON Orders(EntityId);
CREATE INDEX IX_Orders_Created ON Orders(CreatedAt);
GO

CREATE TABLE Settings (
    [Key]   NVARCHAR(100) PRIMARY KEY,
    [Value] NVARCHAR(500) NULL
);
GO

CREATE OR ALTER PROCEDURE sp_CreateOrder
    @EntityId                INT,
    @NotebookCount           INT,
    @AppointmentsPerNotebook INT = 3,
    @OrderId                 INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Total INT = @NotebookCount * @AppointmentsPerNotebook;
        DECLARE @Last  INT;
        DECLARE @YY CHAR(2) = RIGHT(CAST(YEAR(SYSDATETIME()) AS CHAR(4)), 2);

        UPDATE Entities
           SET @Last = LastSequence,
               LastSequence = LastSequence + @Total
         WHERE EntityId = @EntityId;

        IF @@ROWCOUNT = 0 THROW 50001, 'Entity not found.', 1;

        DECLARE @Start CHAR(8) = @YY + RIGHT('000000' + CAST(@Last + 1      AS VARCHAR(6)), 6);
        DECLARE @End   CHAR(8) = @YY + RIGHT('000000' + CAST(@Last + @Total AS VARCHAR(6)), 6);

        INSERT INTO Orders (EntityId, NotebookCount, AppointmentsPerNotebook, StartSerial, EndSerial)
        VALUES (@EntityId, @NotebookCount, @AppointmentsPerNotebook, @Start, @End);

        SET @OrderId = SCOPE_IDENTITY();
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO