using System.Data;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/musteriler")]
public sealed class MusterilerController : ControllerBase
{
    private readonly DovizDbContext _context;

    public MusterilerController(DovizDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> MusteriOlustur(
        MusteriOlusturRequest request,
        CancellationToken cancellationToken)
    {
        var tryDovizId = await _context.Dovizler
            .AsNoTracking()
            .Where(doviz => doviz.Kod == "TRY" && doviz.AktifMi)
            .Select(doviz => (int?)doviz.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (tryDovizId is null)
        {
            return BadRequest(new { mesaj = "Aktif TRY para birimi bulunamadı." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var olusturmaTarihi = DateTime.UtcNow;
        var musteri = new Musteri
        {
            Ad = request.Ad.Trim(),
            Soyad = request.Soyad.Trim(),
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi
        };
        _context.Musteriler.Add(musteri);
        await _context.SaveChangesAsync(cancellationToken);

        var tryHesabi = new MusteriHesabi
        {
            MusteriId = musteri.Id,
            EkNo = 1,
            DovizId = tryDovizId.Value,
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
                musteri.Id,
                musteri.Ad,
                musteri.Soyad,
                tryHesabi.EkNo,
                baslangicTryBakiyesi = tryHesabi.Bakiye,
                musteri.OlusturmaTarihi
            });
    }

    [HttpPost("{musteriId:int}/hesaplar")]
    public async Task<IActionResult> HesapAc(
        int musteriId,
        HesapAcRequest request,
        CancellationToken cancellationToken)
    {
        var musteriVar = await _context.Musteriler
            .AsNoTracking()
            .AnyAsync(
                musteri => musteri.Id == musteriId && musteri.AktifMi,
                cancellationToken);

        if (!musteriVar)
        {
            return NotFound(new { mesaj = "Aktif müşteri bulunamadı." });
        }

        var dovizKodu = request.DovizKodu.Trim().ToUpperInvariant();
        var doviz = await _context.Dovizler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                doviz => doviz.Kod == dovizKodu && doviz.AktifMi,
                cancellationToken);

        if (doviz is null)
        {
            return NotFound(new { mesaj = "Aktif döviz bulunamadı." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sonEkNo = await _context.MusteriHesaplari
            .Where(hesap => hesap.MusteriId == musteriId)
            .Select(hesap => (int?)hesap.EkNo)
            .MaxAsync(cancellationToken) ?? 0;
        var olusturmaTarihi = DateTime.UtcNow;
        var hesap = new MusteriHesabi
        {
            MusteriId = musteriId,
            EkNo = sonEkNo + 1,
            DovizId = doviz.Id,
            Bakiye = 0,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };
        _context.MusteriHesaplari.Add(hesap);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CreatedAtAction(
            nameof(MusteriHesaplariniGetir),
            new { musteriId },
            new
            {
                hesap.Id,
                hesap.EkNo,
                dovizKodu = doviz.Kod,
                dovizAdi = doviz.Ad,
                hesap.Bakiye,
                hesap.AktifMi,
                hesap.OlusturmaTarihi
            });
    }

    [HttpGet]
    public async Task<IActionResult> MusterileriGetir(CancellationToken cancellationToken)
    {
        var musteriler = await _context.Musteriler
            .AsNoTracking()
            .OrderBy(musteri => musteri.Id)
            .Select(musteri => new
            {
                musteri.Id,
                musteri.Ad,
                musteri.Soyad,
                musteri.AktifMi,
                musteri.OlusturmaTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(musteriler);
    }

    [HttpGet("{musteriId:int}/hesaplar")]
    public async Task<IActionResult> MusteriHesaplariniGetir(
        int musteriId,
        CancellationToken cancellationToken)
    {
        var musteri = await _context.Musteriler
            .AsNoTracking()
            .Where(musteri => musteri.Id == musteriId)
            .Select(musteri => new
            {
                musteri.Id,
                musteri.Ad,
                musteri.Soyad,
                musteri.AktifMi
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (musteri is null)
        {
            return NotFound(new { mesaj = "Müşteri bulunamadı." });
        }

        var hesaplar = await _context.MusteriHesaplari
            .AsNoTracking()
            .Where(hesap => hesap.MusteriId == musteriId)
            .OrderBy(hesap => hesap.EkNo)
            .Select(hesap => new
            {
                hesap.Id,
                hesap.EkNo,
                hesap.DovizId,
                dovizKodu = hesap.Doviz.Kod,
                dovizAdi = hesap.Doviz.Ad,
                hesap.Bakiye,
                hesap.AktifMi,
                hesap.OlusturmaTarihi,
                hesap.GuncellemeTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            musteri,
            hesaplar
        });
    }

    [HttpGet("{musteriId:int}/hesaplar/{ekNo:int}/hareketler")]
    public async Task<IActionResult> HesapHareketleriniGetir(
        int musteriId,
        int ekNo,
        CancellationToken cancellationToken)
    {
        var hesap = await _context.MusteriHesaplari
            .AsNoTracking()
            .Where(hesap => hesap.MusteriId == musteriId && hesap.EkNo == ekNo)
            .Select(hesap => new
            {
                hesap.Id,
                hesap.EkNo,
                dovizKodu = hesap.Doviz.Kod,
                dovizAdi = hesap.Doviz.Ad,
                hesap.Bakiye,
                hesap.AktifMi
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (hesap is null)
        {
            return NotFound(new { mesaj = "Müşteriye ait hesap bulunamadı." });
        }

        var hareketler = await _context.HesapHareketleri
            .AsNoTracking()
            .Where(hareket => hareket.HesapId == hesap.Id)
            .OrderByDescending(hareket => hareket.IslemTarihi)
            .Select(hareket => new
            {
                hareket.Id,
                hareket.DovizIslemId,
                hareket.DovizIslemi.ReferansNo,
                hareket.HareketTuru,
                hareket.DovizMiktari,
                hareket.TlKarsiligi,
                hareket.IslemTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            hesap,
            hareketler
        });
    }
}
