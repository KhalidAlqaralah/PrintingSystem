USE Everest;
GO

CREATE OR ALTER PROCEDURE sp_UpdateOrder
    @OrderId       INT,
    @EntityId      INT,
    @NotebookCount INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @OldEntity INT, @OldTotal INT, @OldEnd CHAR(8), @Per INT, @Printed DATETIME2;

        SELECT @OldEntity = EntityId, @OldTotal = TotalAppointments,
               @OldEnd = EndSerial, @Per = AppointmentsPerNotebook, @Printed = PrintedAt
          FROM Orders WHERE OrderId = @OrderId;

        IF @OldEntity IS NULL THROW 50002, 'Order not found.', 1;
        IF @Printed IS NOT NULL THROW 50003, 'PRINTED', 1;

        -- only the newest order of an entity may be edited
        IF EXISTS (SELECT 1 FROM Orders
                   WHERE EntityId = @OldEntity AND OrderId > @OrderId)
            THROW 50004, 'NOTLAST', 1;

        IF @OldEntity <> @EntityId AND
           EXISTS (SELECT 1 FROM Orders WHERE EntityId = @EntityId)
           AND NOT EXISTS (SELECT 1 FROM Orders
                           WHERE EntityId = @EntityId
                             AND OrderId = (SELECT MAX(OrderId) FROM Orders WHERE EntityId = @EntityId))
            THROW 50005, 'TARGETBUSY', 1;

        -- release the old range
        UPDATE Entities
           SET LastSequence = LastSequence - @OldTotal
         WHERE EntityId = @OldEntity
           AND LastSequence = CAST(RIGHT(@OldEnd, 6) AS INT);

        -- reserve the new range
        DECLARE @Total INT = @NotebookCount * @Per;
        DECLARE @Last INT;
        DECLARE @YY CHAR(2) = RIGHT(CAST(YEAR(SYSDATETIME()) AS CHAR(4)), 2);

        UPDATE Entities
           SET @Last = LastSequence,
               LastSequence = LastSequence + @Total
         WHERE EntityId = @EntityId;

        IF @@ROWCOUNT = 0 THROW 50001, 'Entity not found.', 1;

        UPDATE Orders
           SET EntityId      = @EntityId,
               NotebookCount = @NotebookCount,
               StartSerial   = @YY + RIGHT('000000' + CAST(@Last + 1      AS VARCHAR(6)), 6),
               EndSerial     = @YY + RIGHT('000000' + CAST(@Last + @Total AS VARCHAR(6)), 6)
         WHERE OrderId = @OrderId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO