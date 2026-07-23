SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MusteriHesaplari', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DovizIslemleri', N'BorcluHesapId') IS NOT NULL
   AND OBJECT_ID(N'dbo.HesapHareketleri', N'U') IS NOT NULL
BEGIN
    PRINT N'Hesap ve borç/alacak migration işlemi daha önce uygulanmış.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.MusteriBakiyeleri', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.MusteriHesaplari', N'U') IS NULL
    BEGIN
        EXEC sys.sp_rename N'dbo.MusteriBakiyeleri', N'MusteriHesaplari';
    END;

    IF OBJECT_ID(N'dbo.CK_MusteriBakiyeleri_Miktar', N'C') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            DROP CONSTRAINT CK_MusteriBakiyeleri_Miktar;
    END;

    IF COL_LENGTH(N'dbo.MusteriHesaplari', N'Miktar') IS NOT NULL
       AND COL_LENGTH(N'dbo.MusteriHesaplari', N'Bakiye') IS NULL
    BEGIN
        EXEC sys.sp_rename N'dbo.MusteriHesaplari.Miktar', N'Bakiye', N'COLUMN';
    END;

    IF COL_LENGTH(N'dbo.MusteriHesaplari', N'EkNo') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari ADD EkNo INT NULL;
    END;

    IF COL_LENGTH(N'dbo.MusteriHesaplari', N'AktifMi') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            ADD AktifMi BIT NOT NULL
                CONSTRAINT DF_MusteriHesaplari_AktifMi DEFAULT (1);
    END;

    IF COL_LENGTH(N'dbo.MusteriHesaplari', N'OlusturmaTarihi') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari ADD OlusturmaTarihi DATETIME2 NULL;
    END;

    ;WITH NumaraliHesaplar AS
    (
        SELECT
            Id,
            ROW_NUMBER() OVER (PARTITION BY MusteriId ORDER BY Id) AS YeniEkNo
        FROM dbo.MusteriHesaplari
    )
    UPDATE hesap
    SET
        hesap.EkNo = numara.YeniEkNo,
        hesap.OlusturmaTarihi = COALESCE(hesap.OlusturmaTarihi, hesap.GuncellemeTarihi)
    FROM dbo.MusteriHesaplari AS hesap
    INNER JOIN NumaraliHesaplar AS numara ON numara.Id = hesap.Id
    WHERE hesap.EkNo IS NULL OR hesap.OlusturmaTarihi IS NULL;

    ALTER TABLE dbo.MusteriHesaplari ALTER COLUMN EkNo INT NOT NULL;
    ALTER TABLE dbo.MusteriHesaplari ALTER COLUMN OlusturmaTarihi DATETIME2 NOT NULL;

    IF OBJECT_ID(N'dbo.DF_MusteriHesaplari_OlusturmaTarihi', N'D') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            ADD CONSTRAINT DF_MusteriHesaplari_OlusturmaTarihi
                DEFAULT SYSUTCDATETIME() FOR OlusturmaTarihi;
    END;

    IF OBJECT_ID(N'dbo.UQ_MusteriBakiyeleri_Musteri_Doviz', N'UQ') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            DROP CONSTRAINT UQ_MusteriBakiyeleri_Musteri_Doviz;
    END;

    IF OBJECT_ID(N'dbo.CK_MusteriBakiyeleri_Miktar', N'C') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            DROP CONSTRAINT CK_MusteriBakiyeleri_Miktar;
    END;

    IF OBJECT_ID(N'dbo.UQ_MusteriHesaplari_Musteri_EkNo', N'UQ') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            ADD CONSTRAINT UQ_MusteriHesaplari_Musteri_EkNo
                UNIQUE (MusteriId, EkNo);
    END;

    IF OBJECT_ID(N'dbo.CK_MusteriHesaplari_Bakiye', N'C') IS NULL
    BEGIN
        ALTER TABLE dbo.MusteriHesaplari
            ADD CONSTRAINT CK_MusteriHesaplari_Bakiye CHECK (Bakiye >= 0);
    END;

    IF OBJECT_ID(N'dbo.FK_MusteriBakiyeleri_Musteriler', N'F') IS NOT NULL
    BEGIN
        EXEC sys.sp_rename
            N'dbo.FK_MusteriBakiyeleri_Musteriler',
            N'FK_MusteriHesaplari_Musteriler',
            N'OBJECT';
    END;

    IF OBJECT_ID(N'dbo.FK_MusteriBakiyeleri_Dovizler', N'F') IS NOT NULL
    BEGIN
        EXEC sys.sp_rename
            N'dbo.FK_MusteriBakiyeleri_Dovizler',
            N'FK_MusteriHesaplari_Dovizler',
            N'OBJECT';
    END;

    IF COL_LENGTH(N'dbo.DovizIslemleri', N'ReferansNo') IS NULL
    BEGIN
        ALTER TABLE dbo.DovizIslemleri ADD
            ReferansNo UNIQUEIDENTIFIER NULL,
            BorcluHesapId BIGINT NULL,
            AlacakliHesapId BIGINT NULL,
            OdenenDovizMiktari DECIMAL(19,4) NULL,
            AlinanDovizMiktari DECIMAL(19,4) NULL,
            OdenenDovizKuru DECIMAL(19,6) NULL,
            AlinanDovizKuru DECIMAL(19,6) NULL;
    END;

    EXEC sys.sp_executesql N'
        UPDATE islem
        SET
            ReferansNo = COALESCE(islem.ReferansNo, NEWID()),
            BorcluHesapId = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN dovizHesabi.Id
                ELSE tryHesabi.Id
            END,
            AlacakliHesapId = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN tryHesabi.Id
                ELSE dovizHesabi.Id
            END,
            OdenenDovizMiktari = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN islem.TlTutari
                ELSE islem.DovizMiktari
            END,
            AlinanDovizMiktari = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN islem.DovizMiktari
                ELSE islem.TlTutari
            END,
            OdenenDovizKuru = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN 1
                ELSE islem.Kur
            END,
            AlinanDovizKuru = CASE
                WHEN islem.IslemTuru = ''ALIS'' THEN islem.Kur
                ELSE 1
            END
        FROM dbo.DovizIslemleri AS islem
        INNER JOIN dbo.MusteriHesaplari AS dovizHesabi
            ON dovizHesabi.MusteriId = islem.MusteriId
           AND dovizHesabi.DovizId = islem.DovizId
        INNER JOIN dbo.Dovizler AS tryDoviz
            ON tryDoviz.Kod = ''TRY''
        INNER JOIN dbo.MusteriHesaplari AS tryHesabi
            ON tryHesabi.MusteriId = islem.MusteriId
           AND tryHesabi.DovizId = tryDoviz.Id
        WHERE islem.BorcluHesapId IS NULL
           OR islem.AlacakliHesapId IS NULL;
    ';

    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN ReferansNo UNIQUEIDENTIFIER NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN BorcluHesapId BIGINT NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN AlacakliHesapId BIGINT NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN OdenenDovizMiktari DECIMAL(19,4) NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN AlinanDovizMiktari DECIMAL(19,4) NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN OdenenDovizKuru DECIMAL(19,6) NOT NULL;
        ALTER TABLE dbo.DovizIslemleri ALTER COLUMN AlinanDovizKuru DECIMAL(19,6) NOT NULL;
    ';

    IF OBJECT_ID(N'dbo.DF_DovizIslemleri_ReferansNo', N'D') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT DF_DovizIslemleri_ReferansNo DEFAULT NEWID() FOR ReferansNo;
        ';
    END;

    IF OBJECT_ID(N'dbo.FK_DovizIslemleri_Dovizler', N'F') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.DovizIslemleri DROP CONSTRAINT FK_DovizIslemleri_Dovizler;
    END;

    IF OBJECT_ID(N'dbo.CK_DovizIslemleri_IslemTuru', N'C') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.DovizIslemleri DROP CONSTRAINT CK_DovizIslemleri_IslemTuru;
    END;

    IF OBJECT_ID(N'dbo.CK_DovizIslemleri_Tutarlar', N'C') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.DovizIslemleri DROP CONSTRAINT CK_DovizIslemleri_Tutarlar;
    END;

    IF COL_LENGTH(N'dbo.DovizIslemleri', N'TlTutari') IS NOT NULL
       AND COL_LENGTH(N'dbo.DovizIslemleri', N'TlKarsiligi') IS NULL
    BEGIN
        EXEC sys.sp_rename N'dbo.DovizIslemleri.TlTutari', N'TlKarsiligi', N'COLUMN';
    END;

    IF COL_LENGTH(N'dbo.DovizIslemleri', N'DovizId') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.DovizIslemleri
            DROP COLUMN DovizId, IslemTuru, DovizMiktari, Kur;
    END;

    IF OBJECT_ID(N'dbo.UQ_DovizIslemleri_ReferansNo', N'UQ') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT UQ_DovizIslemleri_ReferansNo UNIQUE (ReferansNo);
        ';
    END;

    IF OBJECT_ID(N'dbo.CK_DovizIslemleri_Hesaplar', N'C') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT CK_DovizIslemleri_Hesaplar
                    CHECK (BorcluHesapId <> AlacakliHesapId);
        ';
    END;

    IF OBJECT_ID(N'dbo.CK_DovizIslemleri_Tutarlar', N'C') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT CK_DovizIslemleri_Tutarlar
                    CHECK (
                        OdenenDovizMiktari > 0 AND
                        AlinanDovizMiktari > 0 AND
                        OdenenDovizKuru > 0 AND
                        AlinanDovizKuru > 0 AND
                        TlKarsiligi > 0
                    );
        ';
    END;

    IF OBJECT_ID(N'dbo.FK_DovizIslemleri_BorcluHesap', N'F') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT FK_DovizIslemleri_BorcluHesap
                    FOREIGN KEY (BorcluHesapId) REFERENCES dbo.MusteriHesaplari(Id);
        ';
    END;

    IF OBJECT_ID(N'dbo.FK_DovizIslemleri_AlacakliHesap', N'F') IS NULL
    BEGIN
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.DovizIslemleri
                ADD CONSTRAINT FK_DovizIslemleri_AlacakliHesap
                    FOREIGN KEY (AlacakliHesapId) REFERENCES dbo.MusteriHesaplari(Id);
        ';
    END;

    IF OBJECT_ID(N'dbo.HesapHareketleri', N'U') IS NULL
    BEGIN
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
            CONSTRAINT FK_HesapHareketleri_MusteriHesaplari
                FOREIGN KEY (HesapId) REFERENCES dbo.MusteriHesaplari(Id),
            CONSTRAINT CK_HesapHareketleri_HareketTuru
                CHECK (HareketTuru IN ('BORC', 'ALACAK')),
            CONSTRAINT CK_HesapHareketleri_Tutarlar
                CHECK (DovizMiktari > 0 AND TlKarsiligi > 0)
        );

        CREATE INDEX IX_HesapHareketleri_DovizIslemId
            ON dbo.HesapHareketleri(DovizIslemId);
        CREATE INDEX IX_HesapHareketleri_HesapId
            ON dbo.HesapHareketleri(HesapId);
    END;

    EXEC sys.sp_executesql N'
        INSERT INTO dbo.HesapHareketleri
        (
            DovizIslemId,
            HesapId,
            HareketTuru,
            DovizMiktari,
            TlKarsiligi,
            IslemTarihi
        )
        SELECT
            islem.Id,
            islem.BorcluHesapId,
            ''BORC'',
            islem.AlinanDovizMiktari,
            islem.TlKarsiligi,
            islem.IslemTarihi
        FROM dbo.DovizIslemleri AS islem
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.HesapHareketleri AS hareket
            WHERE hareket.DovizIslemId = islem.Id
              AND hareket.HareketTuru = ''BORC''
        );

        INSERT INTO dbo.HesapHareketleri
        (
            DovizIslemId,
            HesapId,
            HareketTuru,
            DovizMiktari,
            TlKarsiligi,
            IslemTarihi
        )
        SELECT
            islem.Id,
            islem.AlacakliHesapId,
            ''ALACAK'',
            islem.OdenenDovizMiktari,
            islem.TlKarsiligi,
            islem.IslemTarihi
        FROM dbo.DovizIslemleri AS islem
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.HesapHareketleri AS hareket
            WHERE hareket.DovizIslemId = islem.Id
              AND hareket.HareketTuru = ''ALACAK''
        );
    ';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
