using System.Data;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Requests;
using DovizApi.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DovizApi.Services;

public sealed class DovizIslemService : IDovizIslemService
{
    private readonly DovizDbContext _context;
    private readonly ITcmbKurService _tcmbKurService;

    public DovizIslemService(
        DovizDbContext context,
        ITcmbKurService tcmbKurService)
    {
        _context = context;
        _tcmbKurService = tcmbKurService;
    }

    public async Task<DovizCevirSonucu> DovizCevirAsync(
        DovizCevirRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BorcluHesapEkNo == request.AlacakliHesapEkNo)
        {
            return DovizCevirSonucu.Hata("Borçlu ve alacaklı hesap aynı olamaz.");
        }

        var hesaplar = await _context.MusteriHesaplari
            .AsNoTracking()
            .Include(hesap => hesap.Doviz)
            .Include(hesap => hesap.Musteri)
            .ThenInclude(musteri => musteri.Sube)
            .Where(hesap =>
                hesap.MusteriId == request.MusteriId &&
                hesap.Musteri.AktifMi &&
                hesap.Musteri.Sube.AktifMi &&
                hesap.AktifMi &&
                (hesap.HesapEkNo == request.BorcluHesapEkNo ||
                 hesap.HesapEkNo == request.AlacakliHesapEkNo))
            .ToListAsync(cancellationToken);

        var borcluHesapBilgisi = hesaplar.SingleOrDefault(
            hesap => hesap.HesapEkNo == request.BorcluHesapEkNo);
        var alacakliHesapBilgisi = hesaplar.SingleOrDefault(
            hesap => hesap.HesapEkNo == request.AlacakliHesapEkNo);

        if (borcluHesapBilgisi is null || alacakliHesapBilgisi is null)
        {
            return DovizCevirSonucu.Hata(
                "Müşteriye ait aktif borçlu veya alacaklı hesap bulunamadı.",
                bulunamadi: true);
        }

        if (borcluHesapBilgisi.DovizId == alacakliHesapBilgisi.DovizId)
        {
            return DovizCevirSonucu.Hata("Aynı döviz cinsindeki hesaplar arasında döviz dönüşümü yapılamaz.");
        }

        var tcmbKurListesi = await _tcmbKurService.KurlariGetirAsync(cancellationToken);
        var odenenDovizKuru = KurBul(
            alacakliHesapBilgisi.Doviz.Kod,
            tcmbKurListesi,
            satisKuru: false);
        var alinanDovizKuru = KurBul(
            borcluHesapBilgisi.Doviz.Kod,
            tcmbKurListesi,
            satisKuru: true);

        if (odenenDovizKuru is null)
        {
            return DovizCevirSonucu.Hata(
                $"Ödenecek {alacakliHesapBilgisi.Doviz.Kod} için geçerli TCMB alış kuru bulunamadı.");
        }

        if (alinanDovizKuru is null)
        {
            return DovizCevirSonucu.Hata(
                $"Alınacak {borcluHesapBilgisi.Doviz.Kod} için geçerli TCMB satış kuru bulunamadı.");
        }

        var tlKarsiligi = decimal.Round(
            request.OdenecekDovizMiktari * odenenDovizKuru.Value,
            4,
            MidpointRounding.ToZero);
        var alinanDovizMiktari = decimal.Round(
            tlKarsiligi / alinanDovizKuru.Value,
            4,
            MidpointRounding.ToZero);

        if (tlKarsiligi <= 0 || alinanDovizMiktari <= 0)
        {
            return DovizCevirSonucu.Hata("Girilen miktar döviz dönüşümü oluşturmak için yetersiz.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var borcluHesap = await _context.MusteriHesaplari
            .SingleAsync(
                hesap => hesap.MusteriId == request.MusteriId &&
                         hesap.HesapEkNo == request.BorcluHesapEkNo,
                cancellationToken);
        var alacakliHesap = await _context.MusteriHesaplari
            .SingleAsync(
                hesap => hesap.MusteriId == request.MusteriId &&
                         hesap.HesapEkNo == request.AlacakliHesapEkNo,
                cancellationToken);

        if (alacakliHesap.Bakiye < request.OdenecekDovizMiktari)
        {
            return DovizCevirSonucu.Hata(
                $"Ek No {alacakliHesap.HesapEkNo} hesabının bakiyesi yetersiz.");
        }

        var islemTarihi = DateTime.UtcNow;
        var subeKodu = borcluHesapBilgisi.Musteri.Sube.Kod;
        var islemKodu = borcluHesapBilgisi.Doviz.Kod == "TRY"
            ? "DOVA"
            : "DOVS";
        var sayac = await SonrakiReferansSayaciniAlAsync(cancellationToken);
        var referansNo = $"{subeKodu}{islemKodu}{islemTarihi:yy}{sayac:D6}";

        alacakliHesap.Bakiye -= request.OdenecekDovizMiktari;
        alacakliHesap.GuncellemeTarihi = islemTarihi;
        borcluHesap.Bakiye += alinanDovizMiktari;
        borcluHesap.GuncellemeTarihi = islemTarihi;

        var dovizIslemi = new DovizIslemi
        {
            ReferansNo = referansNo,
            MusteriId = request.MusteriId,
            BorcluHesapEkNo = borcluHesap.HesapEkNo,
            AlacakliHesapEkNo = alacakliHesap.HesapEkNo,
            OdenenDovizId = alacakliHesapBilgisi.DovizId,
            AlinanDovizId = borcluHesapBilgisi.DovizId,
            OdenenDovizMiktari = request.OdenecekDovizMiktari,
            AlinanDovizMiktari = alinanDovizMiktari,
            OdenenDovizKuru = odenenDovizKuru.Value,
            AlinanDovizKuru = alinanDovizKuru.Value,
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        };

        dovizIslemi.HesapHareketleri.Add(new HesapHareketi
        {
            MusteriId = request.MusteriId,
            HesapEkNo = borcluHesap.HesapEkNo,
            HareketTuru = "BORC",
            DovizMiktari = alinanDovizMiktari,
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        });
        dovizIslemi.HesapHareketleri.Add(new HesapHareketi
        {
            MusteriId = request.MusteriId,
            HesapEkNo = alacakliHesap.HesapEkNo,
            HareketTuru = "ALACAK",
            DovizMiktari = request.OdenecekDovizMiktari,
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        });
        _context.DovizIslemleri.Add(dovizIslemi);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DovizCevirSonucu.Basari(new DovizCevirResponse
        {
            IslemId = dovizIslemi.Id,
            ReferansNo = dovizIslemi.ReferansNo,
            MusteriId = request.MusteriId,
            OdenenDovizId = alacakliHesapBilgisi.DovizId,
            OdenenDovizKodu = alacakliHesapBilgisi.Doviz.Kod,
            AlinanDovizId = borcluHesapBilgisi.DovizId,
            AlinanDovizKodu = borcluHesapBilgisi.Doviz.Kod,
            BorcluHesap = new HesapTarafiResponse
            {
                HesapEkNo = borcluHesap.HesapEkNo,
                DovizKodu = borcluHesapBilgisi.Doviz.Kod,
                DovizMiktari = alinanDovizMiktari,
                UygulananKur = alinanDovizKuru.Value,
                YeniBakiye = borcluHesap.Bakiye
            },
            AlacakliHesap = new HesapTarafiResponse
            {
                HesapEkNo = alacakliHesap.HesapEkNo,
                DovizKodu = alacakliHesapBilgisi.Doviz.Kod,
                DovizMiktari = request.OdenecekDovizMiktari,
                UygulananKur = odenenDovizKuru.Value,
                YeniBakiye = alacakliHesap.Bakiye
            },
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        });
    }

    public async Task<DovizTersKayitSonucu> TersKayitOlusturAsync(
        string referansNo,
        string iptalNedeni,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var orijinalIslem = await _context.DovizIslemleri
            .Include(x => x.BorcluHesap)
                .ThenInclude(x => x.Doviz)
            .Include(x => x.BorcluHesap)
                .ThenInclude(x => x.Musteri)
                .ThenInclude(x => x.Sube)
            .Include(x => x.AlacakliHesap)
                .ThenInclude(x => x.Doviz)
            .SingleOrDefaultAsync(
                x => x.ReferansNo == referansNo,
                cancellationToken);

        if (orijinalIslem is null)
        {
            return DovizTersKayitSonucu.Hata(
                "Döviz işlemi bulunamadı.",
                bulunamadi: true);
        }

        if (orijinalIslem.OrijinalIslemId is not null)
        {
            return DovizTersKayitSonucu.Hata(
                "Ters kayıt işlemi tekrar ters çevrilemez.",
                cakisma: true);
        }

        var dahaOnceTersKayitOlusturuldu = await _context.DovizIslemleri
            .AnyAsync(
                x => x.OrijinalIslemId == orijinalIslem.Id,
                cancellationToken);

        if (dahaOnceTersKayitOlusturuldu)
        {
            return DovizTersKayitSonucu.Hata(
                "Bu işlem için daha önce ters kayıt oluşturulmuş.",
                cakisma: true);
        }

        var geriAlinacakHesap = orijinalIslem.BorcluHesap;
        var iadeEdilecekHesap = orijinalIslem.AlacakliHesap;

        if (geriAlinacakHesap.Bakiye < orijinalIslem.AlinanDovizMiktari)
        {
            return DovizTersKayitSonucu.Hata(
                $"Ek No {geriAlinacakHesap.HesapEkNo} hesabının ters kayıt için bakiyesi yetersiz.",
                cakisma: true);
        }

        var islemTarihi = DateTime.UtcNow;
        var subeKodu = geriAlinacakHesap.Musteri.Sube.Kod;
        var islemKodu = iadeEdilecekHesap.Doviz.Kod == "TRY"
            ? "DOVA"
            : "DOVS";
        var sayac = await SonrakiReferansSayaciniAlAsync(cancellationToken);
        var tersKayitReferansNo = $"{subeKodu}{islemKodu}{islemTarihi:yy}{sayac:D6}";

        geriAlinacakHesap.Bakiye -= orijinalIslem.AlinanDovizMiktari;
        geriAlinacakHesap.GuncellemeTarihi = islemTarihi;
        iadeEdilecekHesap.Bakiye += orijinalIslem.OdenenDovizMiktari;
        iadeEdilecekHesap.GuncellemeTarihi = islemTarihi;

        var tersKayit = new DovizIslemi
        {
            ReferansNo = tersKayitReferansNo,
            MusteriId = orijinalIslem.MusteriId,
            BorcluHesapEkNo = iadeEdilecekHesap.HesapEkNo,
            AlacakliHesapEkNo = geriAlinacakHesap.HesapEkNo,
            OdenenDovizId = orijinalIslem.AlinanDovizId,
            AlinanDovizId = orijinalIslem.OdenenDovizId,
            OdenenDovizMiktari = orijinalIslem.AlinanDovizMiktari,
            AlinanDovizMiktari = orijinalIslem.OdenenDovizMiktari,
            OdenenDovizKuru = orijinalIslem.AlinanDovizKuru,
            AlinanDovizKuru = orijinalIslem.OdenenDovizKuru,
            TlKarsiligi = orijinalIslem.TlKarsiligi,
            IslemTarihi = islemTarihi,
            OrijinalIslemId = orijinalIslem.Id,
            IptalNedeni = iptalNedeni
        };

        tersKayit.HesapHareketleri.Add(new HesapHareketi
        {
            MusteriId = tersKayit.MusteriId,
            HesapEkNo = iadeEdilecekHesap.HesapEkNo,
            HareketTuru = "BORC",
            DovizMiktari = orijinalIslem.OdenenDovizMiktari,
            TlKarsiligi = orijinalIslem.TlKarsiligi,
            IslemTarihi = islemTarihi
        });
        tersKayit.HesapHareketleri.Add(new HesapHareketi
        {
            MusteriId = tersKayit.MusteriId,
            HesapEkNo = geriAlinacakHesap.HesapEkNo,
            HareketTuru = "ALACAK",
            DovizMiktari = orijinalIslem.AlinanDovizMiktari,
            TlKarsiligi = orijinalIslem.TlKarsiligi,
            IslemTarihi = islemTarihi
        });

        _context.DovizIslemleri.Add(tersKayit);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DovizTersKayitSonucu.Basari(new DovizTersKayitResponse
        {
            IslemId = tersKayit.Id,
            OrijinalReferansNo = orijinalIslem.ReferansNo,
            TersKayitReferansNo = tersKayit.ReferansNo,
            IptalNedeni = tersKayit.IptalNedeni,
            IslemTarihi = tersKayit.IslemTarihi
        });
    }

    private async Task<int> SonrakiReferansSayaciniAlAsync(
        CancellationToken cancellationToken)
    {
        var transaction = _context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Referans sayacı transaction içerisinde alınmalıdır.");

        var connection = _context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT NEXT VALUE FOR dbo.DovizReferansSayaci";

        var sonuc = await command.ExecuteScalarAsync(cancellationToken);
        if (sonuc is null || sonuc == DBNull.Value)
        {
            throw new InvalidOperationException("Referans sayacı alınamadı.");
        }

        return Convert.ToInt32(sonuc);
    }

    private static decimal? KurBul(
        string dovizKodu,
        TcmbKurListesi tcmbKurListesi,
        bool satisKuru)
    {
        if (dovizKodu == "TRY")
        {
            return 1m;
        }

        var tcmbKuru = tcmbKurListesi.Kurlar.SingleOrDefault(kur => kur.Kod == dovizKodu);
        var kurDegeri = satisKuru ? tcmbKuru?.DovizSatis : tcmbKuru?.DovizAlis;

        if (kurDegeri is null || kurDegeri <= 0 || tcmbKuru!.Birim <= 0)
        {
            return null;
        }

        return decimal.Round(
            kurDegeri.Value / tcmbKuru.Birim,
            6,
            MidpointRounding.ToZero);
    }
}
