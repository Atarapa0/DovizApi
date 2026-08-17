/* Migration: AddHataLoglari
   Bu proje mevcut şemayı SQL scriptleriyle yönettiği için yalnızca yeni tabloyu ekler. */

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.HataLoglari', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HataLoglari
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HataLoglari PRIMARY KEY,
        HataId VARCHAR(32) NOT NULL,
        CorrelationId VARCHAR(128) NOT NULL,
        Tarih DATETIME2(7) NOT NULL
            CONSTRAINT DF_HataLoglari_Tarih DEFAULT SYSUTCDATETIME(),
        Seviye VARCHAR(16) NOT NULL,
        HttpStatus INT NOT NULL,
        HataKodu VARCHAR(100) NOT NULL,
        Mesaj NVARCHAR(1000) NOT NULL,
        Detay NVARCHAR(MAX) NULL,
        StackTrace NVARCHAR(MAX) NULL,
        ExceptionTipi NVARCHAR(500) NULL,
        Endpoint NVARCHAR(2048) NULL,
        HttpMethod VARCHAR(16) NULL,
        QueryString NVARCHAR(2048) NULL,
        MusteriId INT NULL,
        KullaniciId NVARCHAR(256) NULL,
        SubeKodu NVARCHAR(20) NULL,
        Ortam NVARCHAR(50) NOT NULL,
        Kaynak NVARCHAR(100) NOT NULL,
        RequestBody NVARCHAR(MAX) NULL
    );

    CREATE UNIQUE INDEX UX_HataLoglari_HataId
        ON dbo.HataLoglari(HataId);
    CREATE INDEX IX_HataLoglari_CorrelationId
        ON dbo.HataLoglari(CorrelationId);
    CREATE INDEX IX_HataLoglari_Tarih
        ON dbo.HataLoglari(Tarih);
    CREATE INDEX IX_HataLoglari_HttpStatus
        ON dbo.HataLoglari(HttpStatus);
    CREATE INDEX IX_HataLoglari_HataKodu
        ON dbo.HataLoglari(HataKodu);
END;

COMMIT TRANSACTION;



/*
Son Konteol için script
*/


SELECT TOP 10 *
FROM dbo.HataLoglari
ORDER BY Tarih DESC;
