using System.Data;
using DovizApi.Data;
using DovizApi.Exceptions;
using DovizApi.Models;
using DovizApi.Requests;
using DovizApi.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/musteriler")]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status503ServiceUnavailable)]
public sealed class MusterilerController : ControllerBase
{
    private const int IlkHesapEkNo = 5001;
    private readonly DovizDbContext _context;

    public MusterilerController(DovizDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MusteriOlustur(
        MusteriOlusturRequest request,
        CancellationToken cancellationToken)
    {
        var subeKodu = request.SubeKodu.Trim().ToUpperInvariant();
        var sube = await _context.Subeler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == subeKodu && x.AktifMi,
                cancellationToken);

        if (sube is null)
        {
            throw new KaynakBulunamadiException("SUBE_BULUNAMADI", "Aktif şube bulunamadı.");
        }

        var tryDoviz = await _context.Dovizler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == "TRY" && x.AktifMi,
                cancellationToken);

        if (tryDoviz is null)
        {
            throw new GecersizIstekException(
                "TRY_DOVIZI_BULUNAMADI",
                "Aktif TRY para birimi bulunamadı.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var olusturmaTarihi = DateTime.UtcNow;
        var musteri = new Musteri
        {
            SubeId = sube.Id,
            Ad = request.Ad.Trim(),
            Soyad = request.Soyad.Trim(),
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };
        var tryHesabi = new MusteriHesabi
        {
            Musteri = musteri,
            HesapEkNo = IlkHesapEkNo,
            DovizId = tryDoviz.Id,
            Bakiye = request.BaslangicTryBakiyesi,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };

        _context.MusteriHesaplari.Add(tryHesabi);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(MusteriHesaplariniGetir),
            new { musteriId = musteri.Id },
            new
            {
                musteri = new
                {
                    musteri.Id,
                    musteri.Ad,
                    musteri.Soyad,
                    musteri.AktifMi,
                    sube = new { sube.Id, sube.Kod, sube.Ad }
                },
                ilkHesap = new
                {
                    tryHesabi.HesapEkNo,
                    tryHesabi.DovizId,
                    dovizKodu = tryDoviz.Kod,
                    dovizAdi = tryDoviz.Ad,
                    tryHesabi.Bakiye,
                    tryHesabi.AktifMi
                }
            });
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<MusteriListeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<MusteriListeResponse>>> MusterileriGetir(
        [FromQuery] MusteriListeQuery query,
        CancellationToken cancellationToken)
    {
        var sorgu = _context.Musteriler.AsNoTracking();
        var arama = query.Arama?.Trim();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            sorgu = sorgu.Where(x =>
                x.Id.ToString().StartsWith(arama) ||
                x.Ad.Contains(arama) ||
                x.Soyad.Contains(arama) ||
                (x.Ad + " " + x.Soyad).Contains(arama));
        }

        if (!string.IsNullOrWhiteSpace(query.SubeKodu))
        {
            var subeKodu = query.SubeKodu.Trim();
            sorgu = sorgu.Where(x => x.Sube.Kod == subeKodu);
        }

        var totalCount = await sorgu.CountAsync(cancellationToken);
        var musteriler = await sorgu
            .OrderBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new MusteriListeResponse
            {
                Id = x.Id,
                Ad = x.Ad,
                Soyad = x.Soyad,
                AktifMi = x.AktifMi,
                Sube = new SubeOzetResponse
                {
                    Id = x.Sube.Id,
                    Kod = x.Sube.Kod,
                    Ad = x.Sube.Ad
                },
                HesapSayisi = x.Hesaplar.Count,
                OlusturmaTarihi = x.OlusturmaTarihi,
                GuncellemeTarihi = x.GuncellemeTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<MusteriListeResponse>.Create(
            musteriler,
            query.Page,
            query.PageSize,
            totalCount));
    }

    [HttpGet("ara")]
    [ProducesResponseType(typeof(IReadOnlyList<MusteriAramaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MusteriAramaResponse>>> MusteriAra(
        [FromQuery] MusteriAraQuery query,
        CancellationToken cancellationToken)
    {
        var arama = query.Q.Trim();
        if (string.IsNullOrWhiteSpace(arama))
        {
            throw new GecersizIstekException("ARAMA_METNI_GECERSIZ", "Arama metni boş olamaz.");
        }
        var musteriler = await _context.Musteriler
            .AsNoTracking()
            .Where(x =>
                x.Id.ToString().StartsWith(arama) ||
                x.Ad.Contains(arama) ||
                x.Soyad.Contains(arama) ||
                (x.Ad + " " + x.Soyad).Contains(arama))
            .OrderBy(x => x.Id)
            .Take(query.Limit)
            .Select(x => new MusteriAramaResponse
            {
                Id = x.Id,
                Ad = x.Ad,
                Soyad = x.Soyad,
                AktifMi = x.AktifMi,
                Sube = new SubeOzetResponse
                {
                    Id = x.Sube.Id,
                    Kod = x.Sube.Kod,
                    Ad = x.Sube.Ad
                },
                HesapSayisi = x.Hesaplar.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(musteriler);
    }

    [HttpGet("{musteriId:int}/hesaplar")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MusteriHesaplariniGetir(
        int musteriId,
        CancellationToken cancellationToken)
    {
        var musteri = await _context.Musteriler
            .AsNoTracking()
            .Where(x => x.Id == musteriId)
            .Select(x => new
            {
                x.Id,
                x.Ad,
                x.Soyad,
                x.AktifMi,
                sube = new { x.Sube.Id, x.Sube.Kod, x.Sube.Ad, x.Sube.AktifMi },
                hesaplar = x.Hesaplar
                    .OrderBy(hesap => hesap.HesapEkNo)
                    .Select(hesap => new
                    {
                        hesap.HesapEkNo,
                        hesap.DovizId,
                        dovizKodu = hesap.Doviz.Kod,
                        dovizAdi = hesap.Doviz.Ad,
                        hesap.Bakiye,
                        hesap.AktifMi,
                        hesap.OlusturmaTarihi,
                        hesap.GuncellemeTarihi
                    })
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (musteri is null)
        {
            throw new KaynakBulunamadiException("MUSTERI_BULUNAMADI", "Müşteri bulunamadı.");
        }

        return Ok(musteri);
    }

    [HttpPost("{musteriId:int}/hesaplar")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HesapAc(
        int musteriId,
        HesapAcRequest request,
        CancellationToken cancellationToken)
    {
        var musteriVar = await _context.Musteriler
            .AsNoTracking()
            .AnyAsync(x => x.Id == musteriId && x.AktifMi, cancellationToken);

        if (!musteriVar)
        {
            throw new KaynakBulunamadiException("MUSTERI_BULUNAMADI", "Aktif müşteri bulunamadı.");
        }

        var dovizKodu = request.DovizKodu.Trim().ToUpperInvariant();
        var doviz = await _context.Dovizler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == dovizKodu && x.AktifMi,
                cancellationToken);

        if (doviz is null)
        {
            throw new KaynakBulunamadiException("DOVIZ_BULUNAMADI", "Aktif döviz bulunamadı.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sonHesapEkNo = await _context.MusteriHesaplari
            .Where(x => x.MusteriId == musteriId)
            .Select(x => (int?)x.HesapEkNo)
            .MaxAsync(cancellationToken) ?? IlkHesapEkNo - 1;
        var olusturmaTarihi = DateTime.UtcNow;
        var hesap = new MusteriHesabi
        {
            MusteriId = musteriId,
            HesapEkNo = sonHesapEkNo + 1,
            DovizId = doviz.Id,
            Bakiye = 0,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };
        _context.MusteriHesaplari.Add(hesap);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created(
            $"/api/v1/musteriler/{musteriId}/hesaplar",
            new
            {
                hesap.MusteriId,
                hesap.HesapEkNo,
                hesap.DovizId,
                dovizKodu = doviz.Kod,
                dovizAdi = doviz.Ad,
                hesap.Bakiye,
                hesap.AktifMi,
                hesap.OlusturmaTarihi
            });
    }

    [HttpGet("{musteriId:int}/hesaplar/{hesapEkNo:int}/hareketler")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> HesapHareketleriniGetir(
        int musteriId,
        int hesapEkNo,
        CancellationToken cancellationToken)
    {
        var hesap = await _context.MusteriHesaplari
            .AsNoTracking()
            .Where(x => x.MusteriId == musteriId && x.HesapEkNo == hesapEkNo)
            .Select(x => new
            {
                x.MusteriId,
                x.HesapEkNo,
                dovizKodu = x.Doviz.Kod,
                dovizAdi = x.Doviz.Ad,
                x.Bakiye,
                x.AktifMi
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (hesap is null)
        {
            throw new KaynakBulunamadiException(
                "HESAP_BULUNAMADI",
                "Müşteriye ait hesap bulunamadı.");
        }

        var hareketler = await _context.HesapHareketleri
            .AsNoTracking()
            .Where(x => x.MusteriId == musteriId && x.HesapEkNo == hesapEkNo)
            .OrderByDescending(x => x.IslemTarihi)
            .Select(x => new
            {
                x.Id,
                x.DovizIslemId,
                x.DovizIslemi.ReferansNo,
                x.HareketTuru,
                x.DovizMiktari,
                x.TlKarsiligi,
                x.IslemTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(new { hesap, hareketler });
    }

    [HttpGet("{musteriId:int}/hesap-hareketleri")]
    [ProducesResponseType(typeof(MusteriHesapHareketleriResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MusteriHesapHareketleriResponse>> TumHesapHareketleriniGetir(
        int musteriId,
        CancellationToken cancellationToken)
    {
        var sonuc = await _context.Musteriler
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == musteriId)
            .Select(x => new MusteriHesapHareketleriResponse
            {
                MusteriId = x.Id,
                Ad = x.Ad,
                Soyad = x.Soyad,
                Hesaplar = x.Hesaplar
                    .OrderBy(hesap => hesap.HesapEkNo)
                    .Select(hesap => new HesapHareketleriResponse
                    {
                        HesapEkNo = hesap.HesapEkNo,
                        DovizId = hesap.DovizId,
                        DovizKodu = hesap.Doviz.Kod,
                        DovizAdi = hesap.Doviz.Ad,
                        Bakiye = hesap.Bakiye,
                        AktifMi = hesap.AktifMi,
                        Hareketler = hesap.Hareketler
                            .OrderByDescending(hareket => hareket.IslemTarihi)
                            .ThenByDescending(hareket => hareket.Id)
                            .Select(hareket => new HesapHareketResponse
                            {
                                Id = hareket.Id,
                                DovizIslemId = hareket.DovizIslemId,
                                ReferansNo = hareket.DovizIslemi.ReferansNo,
                                HareketTuru = hareket.HareketTuru,
                                DovizMiktari = hareket.DovizMiktari,
                                TlKarsiligi = hareket.TlKarsiligi,
                                IslemTarihi = hareket.IslemTarihi
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sonuc is null)
        {
            throw new KaynakBulunamadiException("MUSTERI_BULUNAMADI", "Müşteri bulunamadı.");
        }

        return Ok(sonuc);
    }
}
