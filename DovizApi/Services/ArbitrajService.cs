using DovizApi.Data;
using DovizApi.Requests;
using DovizApi.Responses;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Services;

public sealed class ArbitrajService : IArbitrajService
{
    private readonly DovizDbContext _context;
    private readonly ITcmbKurService _tcmbKurService;

    public ArbitrajService(
        DovizDbContext context,
        ITcmbKurService tcmbKurService)
    {
        _context = context;
        _tcmbKurService = tcmbKurService;
    }

    public async Task<ArbitrajHesaplamaSonucu> ArbitrajHesaplaAsync(
        ArbitrajHesaplaRequest request,
        CancellationToken cancellationToken)
    {
        var baslangicDovizKodu = NormalizeEt(request.BaslangicDovizKodu);
        var birinciAraDovizKodu = NormalizeEt(request.BirinciAraDovizKodu);
        var ikinciAraDovizKodu = NormalizeEt(request.IkinciAraDovizKodu);
        var dovizKodlari = new[]
        {
            baslangicDovizKodu,
            birinciAraDovizKodu,
            ikinciAraDovizKodu
        };

        if (dovizKodlari.Distinct(StringComparer.Ordinal).Count() != dovizKodlari.Length)
        {
            return ArbitrajHesaplamaSonucu.Hata(
                "Başlangıç ve ara dövizlerin üçü de birbirinden farklı olmalıdır.");
        }

        var aktifDovizKodlari = await _context.Dovizler
            .AsNoTracking()
            .Where(doviz => doviz.AktifMi && dovizKodlari.Contains(doviz.Kod))
            .Select(doviz => doviz.Kod)
            .ToListAsync(cancellationToken);

        var bulunamayanDovizKodlari = dovizKodlari
            .Except(aktifDovizKodlari, StringComparer.Ordinal)
            .ToArray();

        if (bulunamayanDovizKodlari.Length > 0)
        {
            return ArbitrajHesaplamaSonucu.Hata(
                $"Aktif döviz bulunamadı: {string.Join(", ", bulunamayanDovizKodlari)}.",
                bulunamadi: true);
        }

        var tcmbKurListesi = await _tcmbKurService.KurlariGetirAsync(cancellationToken);
        var adimlar = new List<ArbitrajAdimiResponse>(3);

        var birinciAdim = DovizDonusumAdimiHesapla(
            1,
            request.BaslangicMiktari,
            baslangicDovizKodu,
            birinciAraDovizKodu,
            tcmbKurListesi);
        if (birinciAdim.HataMesaji is not null)
        {
            return ArbitrajHesaplamaSonucu.Hata(birinciAdim.HataMesaji);
        }

        adimlar.Add(birinciAdim.Veri!);

        var ikinciAdim = DovizDonusumAdimiHesapla(
            2,
            birinciAdim.Veri!.CikisMiktari,
            birinciAraDovizKodu,
            ikinciAraDovizKodu,
            tcmbKurListesi);
        if (ikinciAdim.HataMesaji is not null)
        {
            return ArbitrajHesaplamaSonucu.Hata(ikinciAdim.HataMesaji);
        }

        adimlar.Add(ikinciAdim.Veri!);

        var ucuncuAdim = DovizDonusumAdimiHesapla(
            3,
            ikinciAdim.Veri!.CikisMiktari,
            ikinciAraDovizKodu,
            baslangicDovizKodu,
            tcmbKurListesi);
        if (ucuncuAdim.HataMesaji is not null)
        {
            return ArbitrajHesaplamaSonucu.Hata(ucuncuAdim.HataMesaji);
        }

        adimlar.Add(ucuncuAdim.Veri!);

        var sonMiktar = ucuncuAdim.Veri!.CikisMiktari;
        var karZararTutari = decimal.Round(
            sonMiktar - request.BaslangicMiktari,
            4,
            MidpointRounding.ToZero);
        var karZararOrani = decimal.Round(
            karZararTutari / request.BaslangicMiktari * 100m,
            4,
            MidpointRounding.ToZero);
        var arbitrajFirsatiVarMi = karZararTutari > 0;

        var aciklama = karZararTutari switch
        {
            > 0 => "Bu rota mevcut TCMB kurlarına göre teorik arbitraj fırsatı oluşturmaktadır.",
            < 0 => "Bu rota mevcut TCMB kurlarına göre zarar oluşturmaktadır.",
            _ => "Bu rota mevcut TCMB kurlarına göre başabaş sonuçlanmaktadır."
        };

        return ArbitrajHesaplamaSonucu.Basari(new ArbitrajHesaplaResponse
        {
            KurTarihi = tcmbKurListesi.Tarih,
            BaslangicDovizKodu = baslangicDovizKodu,
            BaslangicMiktari = request.BaslangicMiktari,
            Adimlar = adimlar,
            SonMiktar = sonMiktar,
            KarZararTutari = karZararTutari,
            KarZararOrani = karZararOrani,
            ArbitrajFirsatiVarMi = arbitrajFirsatiVarMi,
            Aciklama = aciklama
        });
    }

    private static DonusumAdimiSonucu DovizDonusumAdimiHesapla(
        int sira,
        decimal girisMiktari,
        string kaynakDovizKodu,
        string hedefDovizKodu,
        TcmbKurListesi tcmbKurListesi)
    {
        var kaynakAlisKuru = KurBul(kaynakDovizKodu, tcmbKurListesi, satisKuru: false);
        if (kaynakAlisKuru is null)
        {
            return DonusumAdimiSonucu.Hata(
                $"{kaynakDovizKodu} için geçerli TCMB alış kuru bulunamadı.");
        }

        var hedefSatisKuru = KurBul(hedefDovizKodu, tcmbKurListesi, satisKuru: true);
        if (hedefSatisKuru is null)
        {
            return DonusumAdimiSonucu.Hata(
                $"{hedefDovizKodu} için geçerli TCMB satış kuru bulunamadı.");
        }

        try
        {
            var tlKarsiligi = decimal.Round(
                girisMiktari * kaynakAlisKuru.Value,
                4,
                MidpointRounding.ToZero);
            var cikisMiktari = decimal.Round(
                tlKarsiligi / hedefSatisKuru.Value,
                4,
                MidpointRounding.ToZero);

            if (tlKarsiligi <= 0 || cikisMiktari <= 0)
            {
                return DonusumAdimiSonucu.Hata(
                    $"{sira}. arbitraj adımında hesaplanabilir bir miktar oluşmadı.");
            }

            return DonusumAdimiSonucu.Basari(new ArbitrajAdimiResponse
            {
                Sira = sira,
                KaynakDovizKodu = kaynakDovizKodu,
                HedefDovizKodu = hedefDovizKodu,
                GirisMiktari = girisMiktari,
                KaynakAlisKuru = kaynakAlisKuru.Value,
                HedefSatisKuru = hedefSatisKuru.Value,
                TlKarsiligi = tlKarsiligi,
                CikisMiktari = cikisMiktari
            });
        }
        catch (OverflowException)
        {
            return DonusumAdimiSonucu.Hata(
                $"{sira}. arbitraj adımındaki miktar hesaplama sınırını aşıyor.");
        }
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

        var tcmbKuru = tcmbKurListesi.Kurlar.SingleOrDefault(
            kur => kur.Kod == dovizKodu);
        var kurDegeri = satisKuru
            ? tcmbKuru?.DovizSatis
            : tcmbKuru?.DovizAlis;

        if (kurDegeri is null || kurDegeri <= 0 || tcmbKuru!.Birim <= 0)
        {
            return null;
        }

        return decimal.Round(
            kurDegeri.Value / tcmbKuru.Birim,
            6,
            MidpointRounding.ToZero);
    }

    private static string NormalizeEt(string dovizKodu) =>
        dovizKodu.Trim().ToUpperInvariant();

    private sealed record DonusumAdimiSonucu(
        ArbitrajAdimiResponse? Veri,
        string? HataMesaji)
    {
        public static DonusumAdimiSonucu Basari(ArbitrajAdimiResponse veri) =>
            new(veri, null);

        public static DonusumAdimiSonucu Hata(string mesaj) =>
            new(null, mesaj);
    }
}
