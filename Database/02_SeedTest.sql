USE Everest;
GO

INSERT INTO Entities (EntityType, Name, LicenseNumber, Major, Address)
VALUES (1, N'د. بلال عبد الحميد احمد الهواري', N'12345', N'اختصاصي معالجة الاورام بالاشعاع',
        N'جبل عمان - 54 شارع ابن خلدون - الخالدي - الطابق 6');
DECLARE @Doc INT = SCOPE_IDENTITY();

INSERT INTO EntityPhones (EntityId, PhoneType, PhoneNumber, SlotIndex) VALUES
 (@Doc, 1, N'0795971463', 1),
 (@Doc, 2, N'0790651971', 1);

INSERT INTO Entities (EntityType, Name, LicenseNumber, Major, Address)
VALUES (2, N'المستشفى الاسلامي - عمان', N'99887', NULL,
        N'عمان - العبدلي - شارع الزبير بن العوام');
DECLARE @Hos INT = SCOPE_IDENTITY();

INSERT INTO EntityPhones (EntityId, PhoneType, PhoneNumber, SlotIndex) VALUES
 (@Hos, 1, N'0793100100', 1),
 (@Hos, 2, N'065101010', 1);
GO