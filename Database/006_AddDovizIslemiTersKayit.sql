SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.DovizIslemleri', N'OrijinalIslemId') IS NULL
BEGIN
    ALTER TABLE dbo.DovizIslemleri
        ADD OrijinalIslemId BIGINT NULL;
END;

IF COL_LENGTH(N'dbo.DovizIslemleri', N'IptalNedeni') IS NULL
BEGIN
    ALTER TABLE dbo.DovizIslemleri
        ADD IptalNedeni NVARCHAR(500) NULL;
END;

GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_DovizIslemleri_TersKayit'
      AND parent_object_id = OBJECT_ID(N'dbo.DovizIslemleri')
)
BEGIN
    ALTER TABLE dbo.DovizIslemleri WITH CHECK
        ADD CONSTRAINT CK_DovizIslemleri_TersKayit
        CHECK
        (
            (OrijinalIslemId IS NULL AND IptalNedeni IS NULL) OR
            (OrijinalIslemId IS NOT NULL AND IptalNedeni IS NOT NULL)
        );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_DovizIslemleri_OrijinalIslem'
      AND parent_object_id = OBJECT_ID(N'dbo.DovizIslemleri')
)
BEGIN
    ALTER TABLE dbo.DovizIslemleri WITH CHECK
        ADD CONSTRAINT FK_DovizIslemleri_OrijinalIslem
        FOREIGN KEY (OrijinalIslemId)
        REFERENCES dbo.DovizIslemleri(Id);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_DovizIslemleri_OrijinalIslemId'
      AND object_id = OBJECT_ID(N'dbo.DovizIslemleri')
)
BEGIN
    CREATE UNIQUE INDEX UX_DovizIslemleri_OrijinalIslemId
        ON dbo.DovizIslemleri(OrijinalIslemId)
        WHERE OrijinalIslemId IS NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_DovizIslemleri_IslemTarihi_Id'
      AND object_id = OBJECT_ID(N'dbo.DovizIslemleri')
)
BEGIN
    CREATE INDEX IX_DovizIslemleri_IslemTarihi_Id
        ON dbo.DovizIslemleri(IslemTarihi DESC, Id DESC);
END;

COMMIT TRANSACTION;

GO
