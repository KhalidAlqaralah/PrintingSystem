USE Everest;
GO
IF NOT EXISTS (SELECT 1 FROM Settings WHERE [Key]='AppointmentsPerNotebook')
    INSERT INTO Settings ([Key],[Value]) VALUES ('AppointmentsPerNotebook','3');
GO