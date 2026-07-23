SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Dovizler
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Kod VARCHAR(3) NOT NULL,
        Ad NVARCHAR(100) NOT NULL,
        Birim SMALLINT NOT NULL DEFAULT 1,
        AktifMi BIT NOT NULL DEFAULT 1,
        OlusturmaTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Dovizler_Kod UNIQUE (Kod),
        CONSTRAINT CK_Dovizler_Birim CHECK (Birim > 0)
    );

    CREATE TABLE dbo.Musteriler
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Ad NVARCHAR(100) NOT NULL,
        Soyad NVARCHAR(100) NOT NULL,
        AktifMi BIT NOT NULL DEFAULT 1,
        OlusturmaTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE TABLE dbo.MusteriBakiyeleri
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        MusteriId INT NOT NULL,
        DovizId INT NOT NULL,
        Miktar DECIMAL(19,4) NOT NULL DEFAULT 0,
        GuncellemeTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MusteriBakiyeleri_Musteriler
            FOREIGN KEY (MusteriId) REFERENCES dbo.Musteriler(Id),
        CONSTRAINT FK_MusteriBakiyeleri_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT UQ_MusteriBakiyeleri_Musteri_Doviz
            UNIQUE (MusteriId, DovizId),
        CONSTRAINT CK_MusteriBakiyeleri_Miktar CHECK (Miktar >= 0)
    );

    CREATE TABLE dbo.KurKayitlari
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        DovizId INT NOT NULL,
        KurTarihi DATE NOT NULL,
        Birim SMALLINT NOT NULL,
        AlisKuru DECIMAL(19,6) NOT NULL,
        SatisKuru DECIMAL(19,6) NOT NULL,
        OlusturmaTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_KurKayitlari_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT UQ_KurKayitlari_Doviz_Tarih UNIQUE (DovizId, KurTarihi),
        CONSTRAINT CK_KurKayitlari_Birim CHECK (Birim > 0),
        CONSTRAINT CK_KurKayitlari_Kurlar CHECK (AlisKuru > 0 AND SatisKuru > 0)
    );

    CREATE TABLE dbo.DovizIslemleri
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        MusteriId INT NOT NULL,
        DovizId INT NOT NULL,
        IslemTuru VARCHAR(5) NOT NULL,
        DovizMiktari DECIMAL(19,4) NOT NULL,
        Kur DECIMAL(19,6) NOT NULL,
        TlTutari DECIMAL(19,4) NOT NULL,
        IslemTarihi DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_DovizIslemleri_Musteriler
            FOREIGN KEY (MusteriId) REFERENCES dbo.Musteriler(Id),
        CONSTRAINT FK_DovizIslemleri_Dovizler
            FOREIGN KEY (DovizId) REFERENCES dbo.Dovizler(Id),
        CONSTRAINT CK_DovizIslemleri_IslemTuru
            CHECK (IslemTuru IN ('ALIS', 'SATIS')),
        CONSTRAINT CK_DovizIslemleri_Tutarlar
            CHECK (DovizMiktari > 0 AND Kur > 0 AND TlTutari > 0)
    );

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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
