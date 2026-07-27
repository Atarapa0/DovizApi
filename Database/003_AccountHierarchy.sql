/*
    DIKKAT: Bu script temiz başlangıç içindir.
    Mevcut müşteri, hesap, döviz işlemi ve hesap hareketi verilerini siler.
    Dovizler ve KurKayitlari tablolarını korur.

    Bu dosya uygulama tarafından otomatik çalıştırılmaz.
    İçeriğini kontrol ettikten sonra SQL Server üzerinde manuel çalıştırmalısınız.
*/

SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Dovizler', N'U') IS NULL
BEGIN
    THROW 50001, N'Dovizler tablosu bulunamadı. Önce 001 ve 002 scriptlerini çalıştırın.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DROP TABLE IF EXISTS dbo.HesapHareketleri;
    DROP TABLE IF EXISTS dbo.DovizIslemleri;
    DROP TABLE IF EXISTS dbo.EkHesaplar;
    DROP TABLE IF EXISTS dbo.MusteriHesaplari;
    DROP TABLE IF EXISTS dbo.MusteriBakiyeleri;
    DROP TABLE IF EXISTS dbo.AnaHesaplar;
    DROP TABLE IF EXISTS dbo.Musteriler;
    DROP TABLE IF EXISTS dbo.Subeler;

    CREATE TABLE dbo.Subeler
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Subeler PRIMARY KEY,
        Kod VARCHAR(10) NOT NULL,
        Ad NVARCHAR(100) NOT NULL,
        AktifMi BIT NOT NULL
            CONSTRAINT DF_Subeler_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Subeler_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_Subeler_Kod UNIQUE (Kod)
    );

    CREATE TABLE dbo.Musteriler
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Musteriler PRIMARY KEY,
        Ad NVARCHAR(100) NOT NULL,
        Soyad NVARCHAR(100) NOT NULL,
        AktifMi BIT NOT NULL
            CONSTRAINT DF_Musteriler_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_Musteriler_OlusturmaTarihi DEFAULT SYSUTCDATETIME()
    );

    CREATE TABLE dbo.AnaHesaplar
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_AnaHesaplar PRIMARY KEY,
        HesapNo VARCHAR(10) NOT NULL,
        MusteriId INT NOT NULL,
        SubeId INT NOT NULL,
        AktifMi BIT NOT NULL
            CONSTRAINT DF_AnaHesaplar_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_AnaHesaplar_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),
        GuncellemeTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_AnaHesaplar_GuncellemeTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_AnaHesaplar_HesapNo UNIQUE (HesapNo),
        CONSTRAINT CK_AnaHesaplar_HesapNo
            CHECK (HesapNo NOT LIKE '%[^0-9]%' AND LEN(HesapNo) = 10),
        CONSTRAINT FK_AnaHesaplar_Musteriler
            FOREIGN KEY (MusteriId) REFERENCES dbo.Musteriler(Id),
        CONSTRAINT FK_AnaHesaplar_Subeler
            FOREIGN KEY (SubeId) REFERENCES dbo.Subeler(Id)
    );

    CREATE INDEX IX_AnaHesaplar_MusteriId ON dbo.AnaHesaplar(MusteriId);
    CREATE INDEX IX_AnaHesaplar_SubeId ON dbo.AnaHesaplar(SubeId);

    CREATE TABLE dbo.EkHesaplar
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_EkHesaplar PRIMARY KEY,
        AnaHesapId BIGINT NOT NULL,
        EkNo INT NOT NULL,
        DovizId INT NOT NULL,
        Bakiye DECIMAL(19,4) NOT NULL
            CONSTRAINT DF_EkHesaplar_Bakiye DEFAULT (0),
        AktifMi BIT NOT NULL
            CONSTRAINT DF_EkHesaplar_AktifMi DEFAULT (1),
        OlusturmaTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_EkHesaplar_OlusturmaTarihi DEFAULT SYSUTCDATETIME(),
        GuncellemeTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_EkHesaplar_GuncellemeTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_EkHesaplar_AnaHesap_EkNo UNIQUE (AnaHesapId, EkNo),
        CONSTRAINT CK_EkHesaplar_EkNo CHECK (EkNo > 0),
        CONSTRAINT CK_EkHesaplar_Bakiye CHECK (Bakiye >= 0),
        CONSTRAINT FK_EkHesaplar_AnaHesaplar
            FOREIGN KEY (AnaHesapId) REFERENCES dbo.AnaHesaplar(Id),
        CONSTRAINT FK_EkHesaplar_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id)
    );

    CREATE INDEX IX_EkHesaplar_DovizId ON dbo.EkHesaplar(DovizId);

    CREATE TABLE dbo.DovizIslemleri
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DovizIslemleri PRIMARY KEY,
        ReferansNo UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_DovizIslemleri_ReferansNo DEFAULT NEWID(),
        BorcluHesapId BIGINT NOT NULL,
        AlacakliHesapId BIGINT NOT NULL,
        OdenenDovizId INT NOT NULL,
        AlinanDovizId INT NOT NULL,
        OdenenDovizMiktari DECIMAL(19,4) NOT NULL,
        AlinanDovizMiktari DECIMAL(19,4) NOT NULL,
        OdenenDovizKuru DECIMAL(19,6) NOT NULL,
        AlinanDovizKuru DECIMAL(19,6) NOT NULL,
        TlKarsiligi DECIMAL(19,4) NOT NULL,
        IslemTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_DovizIslemleri_IslemTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT UQ_DovizIslemleri_ReferansNo UNIQUE (ReferansNo),
        CONSTRAINT CK_DovizIslemleri_Hesaplar
            CHECK (BorcluHesapId <> AlacakliHesapId),
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
        CONSTRAINT FK_DovizIslemleri_BorcluEkHesap
            FOREIGN KEY (BorcluHesapId) REFERENCES dbo.EkHesaplar(Id),
        CONSTRAINT FK_DovizIslemleri_AlacakliEkHesap
            FOREIGN KEY (AlacakliHesapId) REFERENCES dbo.EkHesaplar(Id),
        CONSTRAINT FK_DovizIslemleri_OdenenDoviz
            FOREIGN KEY (OdenenDovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT FK_DovizIslemleri_AlinanDoviz
            FOREIGN KEY (AlinanDovizId) REFERENCES dbo.Dovizler(Id)
    );

    CREATE INDEX IX_DovizIslemleri_BorcluHesapId
        ON dbo.DovizIslemleri(BorcluHesapId);
    CREATE INDEX IX_DovizIslemleri_AlacakliHesapId
        ON dbo.DovizIslemleri(AlacakliHesapId);
    CREATE INDEX IX_DovizIslemleri_OdenenDovizId
        ON dbo.DovizIslemleri(OdenenDovizId);
    CREATE INDEX IX_DovizIslemleri_AlinanDovizId
        ON dbo.DovizIslemleri(AlinanDovizId);

    CREATE TABLE dbo.HesapHareketleri
    (
        Id BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_HesapHareketleri PRIMARY KEY,
        DovizIslemId BIGINT NOT NULL,
        HesapId BIGINT NOT NULL,
        HareketTuru VARCHAR(6) NOT NULL,
        DovizMiktari DECIMAL(19,4) NOT NULL,
        TlKarsiligi DECIMAL(19,4) NOT NULL,
        IslemTarihi DATETIME2 NOT NULL
            CONSTRAINT DF_HesapHareketleri_IslemTarihi DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_HesapHareketleri_DovizIslemleri
            FOREIGN KEY (DovizIslemId) REFERENCES dbo.DovizIslemleri(Id),
        CONSTRAINT FK_HesapHareketleri_EkHesaplar
            FOREIGN KEY (HesapId) REFERENCES dbo.EkHesaplar(Id),
        CONSTRAINT CK_HesapHareketleri_HareketTuru
            CHECK (HareketTuru IN ('BORC', 'ALACAK')),
        CONSTRAINT CK_HesapHareketleri_Tutarlar
            CHECK (DovizMiktari > 0 AND TlKarsiligi > 0)
    );

    CREATE INDEX IX_HesapHareketleri_DovizIslemId
        ON dbo.HesapHareketleri(DovizIslemId);
    CREATE INDEX IX_HesapHareketleri_HesapId
        ON dbo.HesapHareketleri(HesapId);

    INSERT INTO dbo.Subeler (Kod, Ad)
    VALUES ('001', N'Merkez Şube');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
