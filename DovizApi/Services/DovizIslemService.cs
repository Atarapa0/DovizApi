using System.Data;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Requests;
using DovizApi.Responses;
using Microsoft.EntityFrameworkCore;

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

        var hesaplar = await _context.EkHesaplar
            .AsNoTracking()
            .Include(hesap => hesap.Doviz)
            .Where(hesap =>
                hesap.AnaHesap.HesapNo == request.HesapNo &&
                hesap.AnaHesap.AktifMi &&
                hesap.AnaHesap.Musteri.AktifMi &&
                hesap.AktifMi &&
                (hesap.EkNo == request.BorcluHesapEkNo ||
                 hesap.EkNo == request.AlacakliHesapEkNo))
            .ToListAsync(cancellationToken);

        var borcluHesapBilgisi = hesaplar.SingleOrDefault(
            hesap => hesap.EkNo == request.BorcluHesapEkNo);
        var alacakliHesapBilgisi = hesaplar.SingleOrDefault(
            hesap => hesap.EkNo == request.AlacakliHesapEkNo);

        if (borcluHesapBilgisi is null || alacakliHesapBilgisi is null)
        {
            return DovizCevirSonucu.Hata(
                "Ana hesaba ait aktif borçlu veya alacaklı ek hesap bulunamadı.",
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

        var borcluHesap = await _context.EkHesaplar
            .SingleAsync(hesap => hesap.Id == borcluHesapBilgisi.Id, cancellationToken);
        var alacakliHesap = await _context.EkHesaplar
            .SingleAsync(hesap => hesap.Id == alacakliHesapBilgisi.Id, cancellationToken);

        if (alacakliHesap.Bakiye < request.OdenecekDovizMiktari)
        {
            return DovizCevirSonucu.Hata(
                $"Ek No {alacakliHesap.EkNo} hesabının bakiyesi yetersiz.");
        }

        var islemTarihi = DateTime.UtcNow;
        alacakliHesap.Bakiye -= request.OdenecekDovizMiktari;
        alacakliHesap.GuncellemeTarihi = islemTarihi;
        borcluHesap.Bakiye += alinanDovizMiktari;
        borcluHesap.GuncellemeTarihi = islemTarihi;

        var dovizIslemi = new DovizIslemi
        {
            ReferansNo = Guid.NewGuid(),
            BorcluHesapId = borcluHesap.Id,
            AlacakliHesapId = alacakliHesap.Id,
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
            HesapId = borcluHesap.Id,
            HareketTuru = "BORC",
            DovizMiktari = alinanDovizMiktari,
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        });
        dovizIslemi.HesapHareketleri.Add(new HesapHareketi
        {
            HesapId = alacakliHesap.Id,
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
            HesapNo = request.HesapNo,
            OdenenDovizId = alacakliHesapBilgisi.DovizId,
            OdenenDovizKodu = alacakliHesapBilgisi.Doviz.Kod,
            AlinanDovizId = borcluHesapBilgisi.DovizId,
            AlinanDovizKodu = borcluHesapBilgisi.Doviz.Kod,
            BorcluHesap = new HesapTarafiResponse
            {
                EkNo = borcluHesap.EkNo,
                DovizKodu = borcluHesapBilgisi.Doviz.Kod,
                DovizMiktari = alinanDovizMiktari,
                UygulananKur = alinanDovizKuru.Value,
                YeniBakiye = borcluHesap.Bakiye
            },
            AlacakliHesap = new HesapTarafiResponse
            {
                EkNo = alacakliHesap.EkNo,
                DovizKodu = alacakliHesapBilgisi.Doviz.Kod,
                DovizMiktari = request.OdenecekDovizMiktari,
                UygulananKur = odenenDovizKuru.Value,
                YeniBakiye = alacakliHesap.Bakiye
            },
            TlKarsiligi = tlKarsiligi,
            IslemTarihi = islemTarihi
        });
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
