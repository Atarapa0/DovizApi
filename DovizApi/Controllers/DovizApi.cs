using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using DovizApi.Requests;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Responses;
using DovizApi.Services;
using Microsoft.EntityFrameworkCore;
using DovizApi.Exceptions;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1")]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status503ServiceUnavailable)]
public class DovizController : ControllerBase
{
    private static readonly Expression<Func<DovizIslemi, DovizIslemResponse>>
        DovizIslemProjection = islem => new DovizIslemResponse
        {
            Id = islem.Id,
            ReferansNo = islem.ReferansNo,
            MusteriId = islem.MusteriId,
            Musteri = new DovizIslemMusteriResponse
            {
                Id = islem.BorcluHesap.Musteri.Id,
                Ad = islem.BorcluHesap.Musteri.Ad,
                Soyad = islem.BorcluHesap.Musteri.Soyad,
                Sube = new SubeOzetResponse
                {
                    Id = islem.BorcluHesap.Musteri.Sube.Id,
                    Kod = islem.BorcluHesap.Musteri.Sube.Kod,
                    Ad = islem.BorcluHesap.Musteri.Sube.Ad
                }
            },
            OdenenDoviz = new DovizIslemDovizResponse
            {
                Id = islem.OdenenDovizId,
                Kod = islem.OdenenDoviz.Kod,
                Ad = islem.OdenenDoviz.Ad
            },
            AlinanDoviz = new DovizIslemDovizResponse
            {
                Id = islem.AlinanDovizId,
                Kod = islem.AlinanDoviz.Kod,
                Ad = islem.AlinanDoviz.Ad
            },
            BorcluHesap = new DovizIslemHesapResponse
            {
                HesapEkNo = islem.BorcluHesapEkNo,
                DovizKodu = islem.BorcluHesap.Doviz.Kod,
                Miktar = islem.OdenenDovizMiktari,
                Kur = islem.OdenenDovizKuru
            },
            AlacakliHesap = new DovizIslemHesapResponse
            {
                HesapEkNo = islem.AlacakliHesapEkNo,
                DovizKodu = islem.AlacakliHesap.Doviz.Kod,
                Miktar = islem.AlinanDovizMiktari,
                Kur = islem.AlinanDovizKuru
            },
            TlKarsiligi = islem.TlKarsiligi,
            IslemTarihi = islem.IslemTarihi,
            TersKayitMi = islem.OrijinalIslemId != null,
            TersKayitOlusturulduMu = islem.TersKayit != null,
            OrijinalReferansNo = islem.OrijinalIslem == null
                ? null
                : islem.OrijinalIslem.ReferansNo,
            TersKayitReferansNo = islem.TersKayit == null
                ? null
                : islem.TersKayit.ReferansNo,
            IptalNedeni = islem.IptalNedeni ??
                (islem.TersKayit == null ? null : islem.TersKayit.IptalNedeni)
        };

    private readonly DovizDbContext _context;
    private readonly ITcmbKurService _tcmbKurService;
    private readonly IDovizIslemService _dovizIslemService;

    public DovizController(
        DovizDbContext context,
        ITcmbKurService tcmbKurService,
        IDovizIslemService dovizIslemService)
    {
        _context = context;
        _tcmbKurService = tcmbKurService;
        _dovizIslemService = dovizIslemService;
    }

    [HttpGet("kur-oku", Name = "KurOku")]
    [ProducesResponseType(typeof(TcmbKurListesi), StatusCodes.Status200OK)]
    public async Task<IActionResult> KurOku(CancellationToken cancellationToken)
    {
        var kurListesi = await _tcmbKurService.KurlariGetirAsync(cancellationToken);
        return Ok(kurListesi);
    }

    [HttpPost("doviz-cevir", Name = "DovizCevir")]
    [ProducesResponseType(typeof(DovizCevirResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DovizCevir(
        DovizCevirRequest request,
        CancellationToken cancellationToken)
    {
        var sonuc = await _dovizIslemService.DovizCevirAsync(request, cancellationToken);

        if (sonuc.Basarili)
        {
            return Ok(sonuc.Veri);
        }

        if (sonuc.Bulunamadi)
        {
            throw new KaynakBulunamadiException(
                "HESAP_BULUNAMADI",
                sonuc.HataMesaji ?? "Hesap bulunamadı.");
        }

        if (sonuc.HataKodu == "BAKIYE_YETERSIZ")
        {
            throw new IsKuraliException(
                sonuc.HataKodu,
                sonuc.HataMesaji ?? "Borçlu hesabın bakiyesi yetersiz.");
        }

        throw new GecersizIstekException(
            "DOVIZ_DONUSUMU_GECERSIZ",
            sonuc.HataMesaji ?? "Döviz dönüşümü gerçekleştirilemedi.");
    }

    [HttpGet("dovizleri-getir", Name = "DovizleriGetir")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> DovizleriGetir(CancellationToken cancellationToken)
    {
        var dovizler = await _context.Dovizler
            .AsNoTracking()
            .Where(doviz => doviz.AktifMi)
            .OrderBy(doviz => doviz.Id)
            .Select(doviz => new
            {
                doviz.Id,
                kod = doviz.Kod,
                name = doviz.Ad,
                doviz.Birim
            })
            .ToListAsync(cancellationToken);

        return Ok(dovizler);
    }

    [HttpGet("doviz-islemleri-getir", Name = "DovizIslemleriGetir")]
    [ProducesResponseType(typeof(PagedResponse<DovizIslemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<DovizIslemResponse>>> DovizIslemleriGetir(
        [FromQuery] DovizIslemListeQuery query,
        CancellationToken cancellationToken)
    {
        var sorgu = _context.DovizIslemleri.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SubeKodu))
        {
            var filtrelenenSubeKodu = query.SubeKodu.Trim();
            sorgu = sorgu.Where(islem =>
                islem.BorcluHesap.Musteri.Sube.Kod == filtrelenenSubeKodu);
        }

        var totalCount = await sorgu.CountAsync(cancellationToken);
        var islemler = await sorgu
            .OrderByDescending(islem => islem.IslemTarihi)
            .ThenByDescending(islem => islem.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(DovizIslemProjection)
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<DovizIslemResponse>.Create(
            islemler,
            query.Page,
            query.PageSize,
            totalCount));
    }

    [HttpGet("doviz-islemleri/{referansNo}", Name = "DovizIslemDetayi")]
    [ProducesResponseType(typeof(DovizIslemDetayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DovizIslemDetayResponse>> DovizIslemDetayiniGetir(
        string referansNo,
        CancellationToken cancellationToken)
    {
        var filtrelenenReferansNo = referansNo.Trim().ToUpperInvariant();
        var islem = await _context.DovizIslemleri
            .AsNoTracking()
            .Where(x => x.ReferansNo == filtrelenenReferansNo)
            .Select(DovizIslemProjection)
            .SingleOrDefaultAsync(cancellationToken);

        if (islem is null)
        {
            throw new KaynakBulunamadiException(
                "DOVIZ_ISLEMI_BULUNAMADI",
                "Döviz işlemi bulunamadı.");
        }

        var hareketler = await _context.HesapHareketleri
            .AsNoTracking()
            .Where(x => x.DovizIslemId == islem.Id)
            .OrderBy(x => x.Id)
            .Select(x => new HesapHareketResponse
            {
                Id = x.Id,
                DovizIslemId = x.DovizIslemId,
                ReferansNo = x.DovizIslemi.ReferansNo,
                HareketTuru = x.HareketTuru,
                DovizMiktari = x.DovizMiktari,
                TlKarsiligi = x.TlKarsiligi,
                IslemTarihi = x.IslemTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(new DovizIslemDetayResponse
        {
            Islem = islem,
            HesapHareketleri = hareketler
        });
    }

    [HttpPost("doviz-islemleri/{referansNo}/iptal")]
    [ProducesResponseType(typeof(DovizTersKayitResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DovizTersKayitResponse>> DovizIsleminiIptalEt(
        string referansNo,
        IslemIptalRequest request,
        CancellationToken cancellationToken)
    {
        var filtrelenenReferansNo = referansNo.Trim().ToUpperInvariant();
        var iptalNedeni = request.IptalNedeni.Trim();

        if (string.IsNullOrWhiteSpace(iptalNedeni))
        {
            throw new GecersizIstekException("IPTAL_NEDENI_GECERSIZ", "İptal nedeni boş olamaz.");
        }

        var sonuc = await _dovizIslemService.TersKayitOlusturAsync(
            filtrelenenReferansNo,
            iptalNedeni,
            cancellationToken);

        if (sonuc.Basarili)
        {
            return CreatedAtRoute(
                "DovizIslemDetayi",
                new { referansNo = sonuc.Veri!.TersKayitReferansNo },
                sonuc.Veri);
        }

        if (sonuc.Bulunamadi)
        {
            throw new KaynakBulunamadiException(
                "DOVIZ_ISLEMI_BULUNAMADI",
                sonuc.HataMesaji ?? "Döviz işlemi bulunamadı.");
        }

        if (sonuc.Cakisma)
        {
            throw new IsKuraliException(
                "ISLEM_IPTAL_CAKISMASI",
                sonuc.HataMesaji ?? "İşlem iptal edilemedi.");
        }

        throw new GecersizIstekException(
            "ISLEM_IPTAL_EDILEMEDI",
            sonuc.HataMesaji ?? "İşlem iptal edilemedi.");
    }
}
