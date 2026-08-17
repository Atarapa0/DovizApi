/*
    DOVIZ API - TEK DOSYALIK VERITABANI KURULUMU

    Bu script boş bir SQL Server veritabanında:
      1. Tüm tabloları, ilişkileri, index ve constraintleri oluşturur.
      2. Sunum için örnek şube, müşteri, hesap, kur, işlem ve hareket verilerini ekler.

    UYARI:
      - Hedef tablolardan biri zaten varsa script hiçbir değişiklik yapmadan hata verir.
      - Uygulama bu dosyayı otomatik çalıştırmaz; SQL Server üzerinde elle çalıştırılmalıdır.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

IF EXISTS
(
    SELECT 1
    FROM sys.tables
    WHERE [name] IN
    (
        N'Dovizler',
        N'Subeler',
        N'Musteriler',
        N'MusteriHesaplari',
        N'KurKayitlari',
        N'DovizIslemleri',
        N'HesapHareketleri'
    )
)
BEGIN
    THROW 50005, N'Hedef tablolardan en az biri zaten var. Bu script yalnızca boş veritabanında çalıştırılabilir.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Dovizler
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Dovizler PRIMARY KEY,
        Kod VARCHAR(3) NOT NULL,
        Ad NVARCHAR(100) NOT NULL,
        Birim SMALLINT NOT NULL
            CONSTRAINT DF_Dovizler_Birim DEFAULT (1),
        AktifMi BIT NOT NULL
            CONSTRAINT DF_Dovizler_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Dovizler_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_Dovizler_Kod UNIQUE (Kod),
        CONSTRAINT CK_Dovizler_Birim CHECK (Birim > 0)
    );

    CREATE TABLE dbo.Subeler
    (
        Id INT IDENTITY(2324,1) NOT NULL
            CONSTRAINT PK_Subeler PRIMARY KEY,
        Kod VARCHAR(4) NOT NULL,
        Ad NVARCHAR(100) NOT NULL,
        AktifMi BIT NOT NULL
            CONSTRAINT DF_Subeler_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Subeler_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_Subeler_Kod UNIQUE (Kod),
        CONSTRAINT CK_Subeler_Id CHECK (Id BETWEEN 1000 AND 9999),
        CONSTRAINT CK_Subeler_Kod
            CHECK (LEN(Kod) = 4 AND Kod NOT LIKE '%[^0-9]%')
    );

    CREATE TABLE dbo.Musteriler
    (
        Id INT IDENTITY(100000,1) NOT NULL
            CONSTRAINT PK_Musteriler PRIMARY KEY,
        SubeId INT NOT NULL,
        Ad NVARCHAR(100) NOT NULL,
        Soyad NVARCHAR(100) NOT NULL,
        AktifMi BIT NOT NULL
            CONSTRAINT DF_Musteriler_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Musteriler_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),
        GuncellemeTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Musteriler_GuncellemeTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT CK_Musteriler_Id CHECK (Id BETWEEN 100000 AND 999999),
        CONSTRAINT FK_Musteriler_Subeler
            FOREIGN KEY (SubeId) REFERENCES dbo.Subeler(Id)
    );

    CREATE INDEX IX_Musteriler_SubeId ON dbo.Musteriler(SubeId);

    CREATE TABLE dbo.MusteriHesaplari
    (
        MusteriId INT NOT NULL,
        HesapEkNo INT NOT NULL,
        DovizId INT NOT NULL,
        Bakiye DECIMAL(19,4) NOT NULL
            CONSTRAINT DF_MusteriHesaplari_Bakiye DEFAULT (0),
        AktifMi BIT NOT NULL
            CONSTRAINT DF_MusteriHesaplari_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_MusteriHesaplari_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),
        GuncellemeTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_MusteriHesaplari_GuncellemeTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_MusteriHesaplari
            PRIMARY KEY (MusteriId, HesapEkNo),
        CONSTRAINT CK_MusteriHesaplari_HesapEkNo CHECK (HesapEkNo >= 5001),
        CONSTRAINT CK_MusteriHesaplari_Bakiye CHECK (Bakiye >= 0),
        CONSTRAINT FK_MusteriHesaplari_Musteriler
            FOREIGN KEY (MusteriId) REFERENCES dbo.Musteriler(Id),
        CONSTRAINT FK_MusteriHesaplari_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id)
    );

    CREATE INDEX IX_MusteriHesaplari_DovizId ON dbo.MusteriHesaplari(DovizId);

    CREATE TABLE dbo.KurKayitlari
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_KurKayitlari PRIMARY KEY,
        DovizId INT NOT NULL,
        KurTarihi DATE NOT NULL,
        Birim SMALLINT NOT NULL,
        AlisKuru DECIMAL(19,6) NOT NULL,
        SatisKuru DECIMAL(19,6) NOT NULL,
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_KurKayitlari_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_KurKayitlari_Doviz_Tarih UNIQUE (DovizId, KurTarihi),
        CONSTRAINT CK_KurKayitlari_Birim CHECK (Birim > 0),
        CONSTRAINT CK_KurKayitlari_Kurlar CHECK (AlisKuru > 0 AND SatisKuru > 0),
        CONSTRAINT FK_KurKayitlari_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id)
    );

    CREATE TABLE dbo.DovizIslemleri
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DovizIslemleri PRIMARY KEY,
        ReferansNo CHAR(16) NOT NULL,
        MusteriId INT NOT NULL,
        BorcluHesapEkNo INT NOT NULL,
        AlacakliHesapEkNo INT NOT NULL,
        OdenenDovizId INT NOT NULL,
        AlinanDovizId INT NOT NULL,
        OdenenDovizMiktari DECIMAL(19,4) NOT NULL,
        AlinanDovizMiktari DECIMAL(19,4) NOT NULL,
        OdenenDovizKuru DECIMAL(19,6) NOT NULL,
        AlinanDovizKuru DECIMAL(19,6) NOT NULL,
        TlKarsiligi DECIMAL(19,4) NOT NULL,
        IslemTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_DovizIslemleri_IslemTarihi DEFAULT SYSUTCDATETIME(),
        OrijinalIslemId BIGINT NULL,
        IptalNedeni NVARCHAR(500) NULL,

        CONSTRAINT UQ_DovizIslemleri_ReferansNo UNIQUE (ReferansNo),
        CONSTRAINT CK_DovizIslemleri_ReferansNo
            CHECK
            (
                LEFT(ReferansNo, 4) NOT LIKE '%[^0-9]%' AND
                SUBSTRING(ReferansNo, 5, 4) IN ('DOVA', 'DOVS') AND
                SUBSTRING(ReferansNo, 9, 2) NOT LIKE '%[^0-9]%' AND
                RIGHT(ReferansNo, 6) NOT LIKE '%[^0-9]%'
            ),
        CONSTRAINT CK_DovizIslemleri_Hesaplar
            CHECK (BorcluHesapEkNo <> AlacakliHesapEkNo),
        CONSTRAINT CK_DovizIslemleri_Dovizler
            CHECK (OdenenDovizId <> AlinanDovizId),
        CONSTRAINT CK_DovizIslemleri_Tutarlar
            CHECK
            (
                OdenenDovizMiktari > 0 AND
                AlinanDovizMiktari > 0 AND
                OdenenDovizKuru > 0 AND
                AlinanDovizKuru > 0 AND
                TlKarsiligi > 0
            ),
        CONSTRAINT CK_DovizIslemleri_TersKayit
            CHECK
            (
                (OrijinalIslemId IS NULL AND IptalNedeni IS NULL) OR
                (OrijinalIslemId IS NOT NULL AND IptalNedeni IS NOT NULL)
            ),
        CONSTRAINT FK_DovizIslemleri_BorcluHesap
            FOREIGN KEY (MusteriId, BorcluHesapEkNo)
            REFERENCES dbo.MusteriHesaplari(MusteriId, HesapEkNo),
        CONSTRAINT FK_DovizIslemleri_AlacakliHesap
            FOREIGN KEY (MusteriId, AlacakliHesapEkNo)
            REFERENCES dbo.MusteriHesaplari(MusteriId, HesapEkNo),
        CONSTRAINT FK_DovizIslemleri_OdenenDoviz
            FOREIGN KEY (OdenenDovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT FK_DovizIslemleri_AlinanDoviz
            FOREIGN KEY (AlinanDovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT FK_DovizIslemleri_OrijinalIslem
            FOREIGN KEY (OrijinalIslemId) REFERENCES dbo.DovizIslemleri(Id)
    );

    CREATE INDEX IX_DovizIslemleri_BorcluHesap
        ON dbo.DovizIslemleri(MusteriId, BorcluHesapEkNo);
    CREATE INDEX IX_DovizIslemleri_AlacakliHesap
        ON dbo.DovizIslemleri(MusteriId, AlacakliHesapEkNo);
    CREATE INDEX IX_DovizIslemleri_OdenenDovizId
        ON dbo.DovizIslemleri(OdenenDovizId);
    CREATE INDEX IX_DovizIslemleri_AlinanDovizId
        ON dbo.DovizIslemleri(AlinanDovizId);
    CREATE UNIQUE INDEX UX_DovizIslemleri_OrijinalIslemId
        ON dbo.DovizIslemleri(OrijinalIslemId)
        WHERE OrijinalIslemId IS NOT NULL;
    CREATE INDEX IX_DovizIslemleri_IslemTarihi_Id
        ON dbo.DovizIslemleri(IslemTarihi DESC, Id DESC);

    CREATE TABLE dbo.HesapHareketleri
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HesapHareketleri PRIMARY KEY,
        DovizIslemId BIGINT NOT NULL,
        MusteriId INT NOT NULL,
        HesapEkNo INT NOT NULL,
        HareketTuru VARCHAR(6) NOT NULL,
        DovizMiktari DECIMAL(19,4) NOT NULL,
        TlKarsiligi DECIMAL(19,4) NOT NULL,
        IslemTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_HesapHareketleri_IslemTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_HesapHareketleri_DovizIslemleri
            FOREIGN KEY (DovizIslemId) REFERENCES dbo.DovizIslemleri(Id),
        CONSTRAINT FK_HesapHareketleri_MusteriHesaplari
            FOREIGN KEY (MusteriId, HesapEkNo)
            REFERENCES dbo.MusteriHesaplari(MusteriId, HesapEkNo),
        CONSTRAINT CK_HesapHareketleri_HareketTuru
            CHECK (HareketTuru IN ('BORC', 'ALACAK')),
        CONSTRAINT CK_HesapHareketleri_Tutarlar
            CHECK (DovizMiktari > 0 AND TlKarsiligi > 0)
    );

    CREATE INDEX IX_HesapHareketleri_DovizIslemId
        ON dbo.HesapHareketleri(DovizIslemId);
    CREATE INDEX IX_HesapHareketleri_Hesap
        ON dbo.HesapHareketleri(MusteriId, HesapEkNo);

    /*
        Referans biçimi: SSSSIIIIYYNNNNNN
        SSSS   : 4 basamaklı şube kodu
        IIII   : DOVA veya DOVS işlem kodu
        YY     : Yılın son iki basamağı
        NNNNNN : 6 basamaklı sayaç

        Kurulumda dört örnek işlem bulunduğu için uygulama sayacı 5'ten devam eder.
    */
    CREATE SEQUENCE dbo.DovizReferansSayaci
        AS INT
        START WITH 5
        INCREMENT BY 1
        MINVALUE 1
        MAXVALUE 999999
        NO CYCLE;

    /* Temel dövizler */
    INSERT INTO dbo.Dovizler (Kod, Ad, Birim)
    VALUES
        ('TRY', N'Türk Lirası', 1),
        ('EUR', N'Avrupa Para Birimi', 1),
        ('USD', N'Amerikan Doları', 1),
        ('GBP', N'İngiliz Sterlini', 1),
        ('CHF', N'İsviçre Frangı', 1),
        ('JPY', N'Japon Yeni', 100),
        ('SAR', N'Suudi Arabistan Riyali', 1),
        ('CAD', N'Kanada Doları', 1);

    DECLARE @TryDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'TRY');
    DECLARE @UsdDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'USD');
    DECLARE @EurDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'EUR');
    DECLARE @GbpDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'GBP');

    /* Şubeler */
    INSERT INTO dbo.Subeler (Kod, Ad)
    VALUES
        ('2324', N'Merkez Şube'),
        ('2325', N'Kadıköy Şubesi'),
        ('2326', N'Ankara Çankaya Şubesi');

    DECLARE @MerkezSubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '2324');
    DECLARE @KadikoySubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '2325');
    DECLARE @AnkaraSubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '2326');

    /* Müşteriler */
    INSERT INTO dbo.Musteriler (SubeId, Ad, Soyad)
    VALUES
        (@MerkezSubeId, N'Ayşe', N'Yılmaz'),
        (@KadikoySubeId, N'Mehmet', N'Kaya'),
        (@AnkaraSubeId, N'Elif', N'Demir'),
        (@MerkezSubeId, N'Can', N'Arslan');

    DECLARE @AyseMusteriId INT =
        (SELECT Id FROM dbo.Musteriler WHERE Ad = N'Ayşe' AND Soyad = N'Yılmaz');
    DECLARE @MehmetMusteriId INT =
        (SELECT Id FROM dbo.Musteriler WHERE Ad = N'Mehmet' AND Soyad = N'Kaya');
    DECLARE @ElifMusteriId INT =
        (SELECT Id FROM dbo.Musteriler WHERE Ad = N'Elif' AND Soyad = N'Demir');
    DECLARE @CanMusteriId INT =
        (SELECT Id FROM dbo.Musteriler WHERE Ad = N'Can' AND Soyad = N'Arslan');

    /* Müşteri döviz hesapları: her müşteride numaralandırma 5001'den başlar. */
    INSERT INTO dbo.MusteriHesaplari (MusteriId, HesapEkNo, DovizId, Bakiye)
    VALUES
        (@AyseMusteriId, 5001, @TryDovizId, 85000.0000),
        (@AyseMusteriId, 5002, @UsdDovizId, 1750.0000),
        (@AyseMusteriId, 5003, @EurDovizId, 960.0000),

        (@MehmetMusteriId, 5001, @TryDovizId, 42500.0000),
        (@MehmetMusteriId, 5002, @EurDovizId, 2850.0000),
        (@MehmetMusteriId, 5003, @GbpDovizId, 430.0000),

        (@ElifMusteriId, 5001, @TryDovizId, 123500.0000),
        (@ElifMusteriId, 5002, @UsdDovizId, 4100.0000),

        (@CanMusteriId, 5001, @TryDovizId, 12000.0000),
        (@CanMusteriId, 5002, @UsdDovizId, 600.0000),
        (@CanMusteriId, 5003, @EurDovizId, 350.0000);

    /* Sunum tarihine ait örnek kurlar */
    INSERT INTO dbo.KurKayitlari
        (DovizId, KurTarihi, Birim, AlisKuru, SatisKuru)
    VALUES
        (@UsdDovizId, '2026-07-25', 1, 32.200000, 32.500000),
        (@EurDovizId, '2026-07-25', 1, 34.700000, 35.000000),
        (@GbpDovizId, '2026-07-25', 1, 40.800000, 41.250000);

    /* Referans numaralı örnek döviz işlemleri */
    DECLARE @Islem1Referans CHAR(16) = '2324DOVA26000001';
    DECLARE @Islem2Referans CHAR(16) = '2324DOVS26000002';
    DECLARE @Islem3Referans CHAR(16) = '2325DOVA26000003';
    DECLARE @Islem4Referans CHAR(16) = '2326DOVS26000004';

    INSERT INTO dbo.DovizIslemleri
    (
        ReferansNo, MusteriId, BorcluHesapEkNo, AlacakliHesapEkNo,
        OdenenDovizId, AlinanDovizId,
        OdenenDovizMiktari, AlinanDovizMiktari,
        OdenenDovizKuru, AlinanDovizKuru, TlKarsiligi, IslemTarihi
    )
    VALUES
        (@Islem1Referans, @AyseMusteriId, 5001, 5002,
         @TryDovizId, @UsdDovizId, 32500.0000, 1000.0000,
         1.000000, 32.500000, 32500.0000, '2026-07-25T10:15:00'),

        (@Islem2Referans, @AyseMusteriId, 5002, 5003,
         @UsdDovizId, @EurDovizId, 500.0000, 460.0000,
         32.200000, 35.000000, 16100.0000, '2026-07-25T11:30:00'),

        (@Islem3Referans, @MehmetMusteriId, 5001, 5002,
         @TryDovizId, @EurDovizId, 17500.0000, 500.0000,
         1.000000, 35.000000, 17500.0000, '2026-07-26T09:45:00'),

        (@Islem4Referans, @ElifMusteriId, 5002, 5001,
         @UsdDovizId, @TryDovizId, 1000.0000, 32200.0000,
         32.200000, 1.000000, 32200.0000, '2026-07-26T14:20:00');

    DECLARE @Islem1Id BIGINT =
        (SELECT Id FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem1Referans);
    DECLARE @Islem2Id BIGINT =
        (SELECT Id FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem2Referans);
    DECLARE @Islem3Id BIGINT =
        (SELECT Id FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem3Referans);
    DECLARE @Islem4Id BIGINT =
        (SELECT Id FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem4Referans);

    INSERT INTO dbo.HesapHareketleri
        (DovizIslemId, MusteriId, HesapEkNo, HareketTuru, DovizMiktari, TlKarsiligi, IslemTarihi)
    VALUES
        (@Islem1Id, @AyseMusteriId, 5001, 'BORC', 32500.0000, 32500.0000, '2026-07-25T10:15:00'),
        (@Islem1Id, @AyseMusteriId, 5002, 'ALACAK', 1000.0000, 32500.0000, '2026-07-25T10:15:00'),

        (@Islem2Id, @AyseMusteriId, 5002, 'BORC', 500.0000, 16100.0000, '2026-07-25T11:30:00'),
        (@Islem2Id, @AyseMusteriId, 5003, 'ALACAK', 460.0000, 16100.0000, '2026-07-25T11:30:00'),

        (@Islem3Id, @MehmetMusteriId, 5001, 'BORC', 17500.0000, 17500.0000, '2026-07-26T09:45:00'),
        (@Islem3Id, @MehmetMusteriId, 5002, 'ALACAK', 500.0000, 17500.0000, '2026-07-26T09:45:00'),

        (@Islem4Id, @ElifMusteriId, 5002, 'BORC', 1000.0000, 32200.0000, '2026-07-26T14:20:00'),
        (@Islem4Id, @ElifMusteriId, 5001, 'ALACAK', 32200.0000, 32200.0000, '2026-07-26T14:20:00');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

/* Kurulum sonrası kayıt özeti */
SELECT N'Dovizler' AS Tablo, COUNT(*) AS KayitSayisi FROM dbo.Dovizler
UNION ALL SELECT N'Subeler', COUNT(*) FROM dbo.Subeler
UNION ALL SELECT N'Musteriler', COUNT(*) FROM dbo.Musteriler
UNION ALL SELECT N'MusteriHesaplari', COUNT(*) FROM dbo.MusteriHesaplari
UNION ALL SELECT N'KurKayitlari', COUNT(*) FROM dbo.KurKayitlari
UNION ALL SELECT N'DovizIslemleri', COUNT(*) FROM dbo.DovizIslemleri
UNION ALL SELECT N'HesapHareketleri', COUNT(*) FROM dbo.HesapHareketleri;
