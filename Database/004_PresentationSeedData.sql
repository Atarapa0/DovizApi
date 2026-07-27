/*
    SUNUM VERILERI

    Bu script 003_AccountHierarchy.sql çalıştırıldıktan sonra elle çalıştırılmalıdır.
    Uygulama bu dosyayı otomatik çalıştırmaz.

    Script aynı hesap numaralarını, şube kodlarını, kur kayıtlarını ve işlem
    referanslarını kontrol ettiği için tekrar çalıştırıldığında kayıtları çoğaltmaz.
*/

SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Subeler', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AnaHesaplar', N'U') IS NULL
   OR OBJECT_ID(N'dbo.EkHesaplar', N'U') IS NULL
   OR COL_LENGTH(N'dbo.DovizIslemleri', N'OdenenDovizId') IS NULL
BEGIN
    THROW 50002, N'Güncel hesap tabloları bulunamadı. Önce 003_AccountHierarchy.sql dosyasını çalıştırın.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Sunumda kullanılacak dövizler eksikse tamamlanır. */
    INSERT INTO dbo.Dovizler (Kod, Ad, Birim)
    SELECT kaynak.Kod, kaynak.Ad, kaynak.Birim
    FROM
    (
        VALUES
            ('TRY', N'Türk Lirası', CAST(1 AS SMALLINT)),
            ('USD', N'Amerikan Doları', CAST(1 AS SMALLINT)),
            ('EUR', N'Avrupa Para Birimi', CAST(1 AS SMALLINT)),
            ('GBP', N'İngiliz Sterlini', CAST(1 AS SMALLINT))
    ) AS kaynak(Kod, Ad, Birim)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Dovizler AS mevcut
        WHERE mevcut.Kod = kaynak.Kod
    );

    DECLARE @TryDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'TRY');
    DECLARE @UsdDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'USD');
    DECLARE @EurDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'EUR');
    DECLARE @GbpDovizId INT = (SELECT Id FROM dbo.Dovizler WHERE Kod = 'GBP');

    /* 003 scriptindeki 001 şubesine ek olarak iki sunum şubesi oluşturulur. */
    INSERT INTO dbo.Subeler (Kod, Ad)
    SELECT kaynak.Kod, kaynak.Ad
    FROM
    (
        VALUES
            ('001', N'Merkez Şube'),
            ('002', N'Kadıköy Şubesi'),
            ('003', N'Ankara Çankaya Şubesi')
    ) AS kaynak(Kod, Ad)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Subeler AS mevcut
        WHERE mevcut.Kod = kaynak.Kod
    );

    DECLARE @MerkezSubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '001');
    DECLARE @KadikoySubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '002');
    DECLARE @AnkaraSubeId INT = (SELECT Id FROM dbo.Subeler WHERE Kod = '003');

    /* Her müşteri için ana hesap ve farklı dövizlerde ek hesaplar oluşturulur. */
    IF NOT EXISTS (SELECT 1 FROM dbo.AnaHesaplar WHERE HesapNo = '1000000001')
    BEGIN
        INSERT INTO dbo.Musteriler (Ad, Soyad)
        VALUES (N'Ayşe', N'Yılmaz');

        DECLARE @AyseMusteriId INT = SCOPE_IDENTITY();

        INSERT INTO dbo.AnaHesaplar (HesapNo, MusteriId, SubeId)
        VALUES ('1000000001', @AyseMusteriId, @MerkezSubeId);

        DECLARE @AyseAnaHesapId BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.EkHesaplar (AnaHesapId, EkNo, DovizId, Bakiye)
        VALUES
            (@AyseAnaHesapId, 1, @TryDovizId, 85000.0000),
            (@AyseAnaHesapId, 2, @UsdDovizId, 1750.0000),
            (@AyseAnaHesapId, 3, @EurDovizId, 960.0000);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AnaHesaplar WHERE HesapNo = '1000000002')
    BEGIN
        INSERT INTO dbo.Musteriler (Ad, Soyad)
        VALUES (N'Mehmet', N'Kaya');

        DECLARE @MehmetMusteriId INT = SCOPE_IDENTITY();

        INSERT INTO dbo.AnaHesaplar (HesapNo, MusteriId, SubeId)
        VALUES ('1000000002', @MehmetMusteriId, @KadikoySubeId);

        DECLARE @MehmetAnaHesapId BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.EkHesaplar (AnaHesapId, EkNo, DovizId, Bakiye)
        VALUES
            (@MehmetAnaHesapId, 1, @TryDovizId, 42500.0000),
            (@MehmetAnaHesapId, 2, @EurDovizId, 2850.0000),
            (@MehmetAnaHesapId, 3, @GbpDovizId, 430.0000);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AnaHesaplar WHERE HesapNo = '1000000003')
    BEGIN
        INSERT INTO dbo.Musteriler (Ad, Soyad)
        VALUES (N'Elif', N'Demir');

        DECLARE @ElifMusteriId INT = SCOPE_IDENTITY();

        INSERT INTO dbo.AnaHesaplar (HesapNo, MusteriId, SubeId)
        VALUES ('1000000003', @ElifMusteriId, @AnkaraSubeId);

        DECLARE @ElifAnaHesapId BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.EkHesaplar (AnaHesapId, EkNo, DovizId, Bakiye)
        VALUES
            (@ElifAnaHesapId, 1, @TryDovizId, 123500.0000),
            (@ElifAnaHesapId, 2, @UsdDovizId, 4100.0000);
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.AnaHesaplar WHERE HesapNo = '1000000004')
    BEGIN
        INSERT INTO dbo.Musteriler (Ad, Soyad)
        VALUES (N'Can', N'Arslan');

        DECLARE @CanMusteriId INT = SCOPE_IDENTITY();

        INSERT INTO dbo.AnaHesaplar (HesapNo, MusteriId, SubeId)
        VALUES ('1000000004', @CanMusteriId, @MerkezSubeId);

        DECLARE @CanAnaHesapId BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.EkHesaplar (AnaHesapId, EkNo, DovizId, Bakiye)
        VALUES
            (@CanAnaHesapId, 1, @TryDovizId, 12000.0000),
            (@CanAnaHesapId, 2, @UsdDovizId, 600.0000),
            (@CanAnaHesapId, 3, @EurDovizId, 350.0000);
    END;

    /* Sunum tarihine ait örnek kur kayıtları. */
    INSERT INTO dbo.KurKayitlari
        (DovizId, KurTarihi, Birim, AlisKuru, SatisKuru)
    SELECT kaynak.DovizId, kaynak.KurTarihi, kaynak.Birim, kaynak.AlisKuru, kaynak.SatisKuru
    FROM
    (
        VALUES
            (@UsdDovizId, CONVERT(DATE, '2026-07-25'), CAST(1 AS SMALLINT), CAST(32.200000 AS DECIMAL(19,6)), CAST(32.500000 AS DECIMAL(19,6))),
            (@EurDovizId, CONVERT(DATE, '2026-07-25'), CAST(1 AS SMALLINT), CAST(34.700000 AS DECIMAL(19,6)), CAST(35.000000 AS DECIMAL(19,6))),
            (@GbpDovizId, CONVERT(DATE, '2026-07-25'), CAST(1 AS SMALLINT), CAST(40.800000 AS DECIMAL(19,6)), CAST(41.250000 AS DECIMAL(19,6)))
    ) AS kaynak(DovizId, KurTarihi, Birim, AlisKuru, SatisKuru)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.KurKayitlari AS mevcut
        WHERE mevcut.DovizId = kaynak.DovizId
          AND mevcut.KurTarihi = kaynak.KurTarihi
    );

    /* İşlem 1: Ayşe Yılmaz, 32.500 TRY ödeyerek 1.000 USD alıyor. */
    DECLARE @Islem1Referans UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
    IF NOT EXISTS (SELECT 1 FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem1Referans)
    BEGIN
        DECLARE @AyseTryHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000001' AND ek.EkNo = 1
        );
        DECLARE @AyseUsdHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000001' AND ek.EkNo = 2
        );

        INSERT INTO dbo.DovizIslemleri
        (
            ReferansNo, BorcluHesapId, AlacakliHesapId,
            OdenenDovizId, AlinanDovizId,
            OdenenDovizMiktari, AlinanDovizMiktari,
            OdenenDovizKuru, AlinanDovizKuru,
            TlKarsiligi, IslemTarihi
        )
        VALUES
        (
            @Islem1Referans, @AyseUsdHesapId, @AyseTryHesapId,
            @TryDovizId, @UsdDovizId,
            32500.0000, 1000.0000,
            1.000000, 32.500000,
            32500.0000, '2026-07-25T10:15:00'
        );

        DECLARE @Islem1Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.HesapHareketleri
            (DovizIslemId, HesapId, HareketTuru, DovizMiktari, TlKarsiligi, IslemTarihi)
        VALUES
            (@Islem1Id, @AyseUsdHesapId, 'BORC', 1000.0000, 32500.0000, '2026-07-25T10:15:00'),
            (@Islem1Id, @AyseTryHesapId, 'ALACAK', 32500.0000, 32500.0000, '2026-07-25T10:15:00');
    END;

    /* İşlem 2: Ayşe Yılmaz, 500 USD ödeyerek 460 EUR alıyor. */
    DECLARE @Islem2Referans UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
    IF NOT EXISTS (SELECT 1 FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem2Referans)
    BEGIN
        DECLARE @AyseUsdHesapId2 BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000001' AND ek.EkNo = 2
        );
        DECLARE @AyseEurHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000001' AND ek.EkNo = 3
        );

        INSERT INTO dbo.DovizIslemleri
        (
            ReferansNo, BorcluHesapId, AlacakliHesapId,
            OdenenDovizId, AlinanDovizId,
            OdenenDovizMiktari, AlinanDovizMiktari,
            OdenenDovizKuru, AlinanDovizKuru,
            TlKarsiligi, IslemTarihi
        )
        VALUES
        (
            @Islem2Referans, @AyseEurHesapId, @AyseUsdHesapId2,
            @UsdDovizId, @EurDovizId,
            500.0000, 460.0000,
            32.200000, 35.000000,
            16100.0000, '2026-07-25T11:30:00'
        );

        DECLARE @Islem2Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.HesapHareketleri
            (DovizIslemId, HesapId, HareketTuru, DovizMiktari, TlKarsiligi, IslemTarihi)
        VALUES
            (@Islem2Id, @AyseEurHesapId, 'BORC', 460.0000, 16100.0000, '2026-07-25T11:30:00'),
            (@Islem2Id, @AyseUsdHesapId2, 'ALACAK', 500.0000, 16100.0000, '2026-07-25T11:30:00');
    END;

    /* İşlem 3: Mehmet Kaya, 17.500 TRY ödeyerek 500 EUR alıyor. */
    DECLARE @Islem3Referans UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';
    IF NOT EXISTS (SELECT 1 FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem3Referans)
    BEGIN
        DECLARE @MehmetTryHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000002' AND ek.EkNo = 1
        );
        DECLARE @MehmetEurHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000002' AND ek.EkNo = 2
        );

        INSERT INTO dbo.DovizIslemleri
        (
            ReferansNo, BorcluHesapId, AlacakliHesapId,
            OdenenDovizId, AlinanDovizId,
            OdenenDovizMiktari, AlinanDovizMiktari,
            OdenenDovizKuru, AlinanDovizKuru,
            TlKarsiligi, IslemTarihi
        )
        VALUES
        (
            @Islem3Referans, @MehmetEurHesapId, @MehmetTryHesapId,
            @TryDovizId, @EurDovizId,
            17500.0000, 500.0000,
            1.000000, 35.000000,
            17500.0000, '2026-07-26T09:45:00'
        );

        DECLARE @Islem3Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.HesapHareketleri
            (DovizIslemId, HesapId, HareketTuru, DovizMiktari, TlKarsiligi, IslemTarihi)
        VALUES
            (@Islem3Id, @MehmetEurHesapId, 'BORC', 500.0000, 17500.0000, '2026-07-26T09:45:00'),
            (@Islem3Id, @MehmetTryHesapId, 'ALACAK', 17500.0000, 17500.0000, '2026-07-26T09:45:00');
    END;

    /* İşlem 4: Elif Demir, 1.000 USD bozdurarak 32.200 TRY alıyor. */
    DECLARE @Islem4Referans UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000004';
    IF NOT EXISTS (SELECT 1 FROM dbo.DovizIslemleri WHERE ReferansNo = @Islem4Referans)
    BEGIN
        DECLARE @ElifTryHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000003' AND ek.EkNo = 1
        );
        DECLARE @ElifUsdHesapId BIGINT =
        (
            SELECT ek.Id
            FROM dbo.EkHesaplar AS ek
            INNER JOIN dbo.AnaHesaplar AS ana ON ana.Id = ek.AnaHesapId
            WHERE ana.HesapNo = '1000000003' AND ek.EkNo = 2
        );

        INSERT INTO dbo.DovizIslemleri
        (
            ReferansNo, BorcluHesapId, AlacakliHesapId,
            OdenenDovizId, AlinanDovizId,
            OdenenDovizMiktari, AlinanDovizMiktari,
            OdenenDovizKuru, AlinanDovizKuru,
            TlKarsiligi, IslemTarihi
        )
        VALUES
        (
            @Islem4Referans, @ElifTryHesapId, @ElifUsdHesapId,
            @UsdDovizId, @TryDovizId,
            1000.0000, 32200.0000,
            32.200000, 1.000000,
            32200.0000, '2026-07-26T14:20:00'
        );

        DECLARE @Islem4Id BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.HesapHareketleri
            (DovizIslemId, HesapId, HareketTuru, DovizMiktari, TlKarsiligi, IslemTarihi)
        VALUES
            (@Islem4Id, @ElifTryHesapId, 'BORC', 32200.0000, 32200.0000, '2026-07-26T14:20:00'),
            (@Islem4Id, @ElifUsdHesapId, 'ALACAK', 1000.0000, 32200.0000, '2026-07-26T14:20:00');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
