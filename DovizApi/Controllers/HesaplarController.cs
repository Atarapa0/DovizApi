using System.Data;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/hesaplar")]
public sealed class HesaplarController : ControllerBase
{
    private readonly DovizDbContext _context;

    public HesaplarController(DovizDbContext context)
    {
        _context = context;
    }

    [HttpGet("{hesapNo}")]
    public async Task<IActionResult> HesabiGetir(
        string hesapNo,
        CancellationToken cancellationToken)
    {
        var hesap = await _context.AnaHesaplar
            .AsNoTracking()
            .Where(x => x.HesapNo == hesapNo)
            .Select(x => new
            {
                x.Id,
                x.HesapNo,
                x.AktifMi,
                x.OlusturmaTarihi,
                x.GuncellemeTarihi,
                musteri = new
                {
                    x.Musteri.Id,
                    x.Musteri.Ad,
                    x.Musteri.Soyad,
                    x.Musteri.AktifMi
                },
                sube = new
                {
                    x.Sube.Id,
                    x.Sube.Kod,
                    x.Sube.Ad,
                    x.Sube.AktifMi
                },
                ekHesaplar = x.EkHesaplar
                    .OrderBy(ekHesap => ekHesap.EkNo)
                    .Select(ekHesap => new
                    {
                        ekHesap.Id,
                        ekHesap.EkNo,
                        ekHesap.DovizId,
                        dovizKodu = ekHesap.Doviz.Kod,
                        dovizAdi = ekHesap.Doviz.Ad,
                        ekHesap.Bakiye,
                        ekHesap.AktifMi,
                        ekHesap.OlusturmaTarihi,
                        ekHesap.GuncellemeTarihi
                    })
            })
            .SingleOrDefaultAsync(cancellationToken);

        return hesap is null
            ? NotFound(new { mesaj = "Ana hesap bulunamadı." })
            : Ok(hesap);
    }

    [HttpPost("{hesapNo}/ek-hesaplar")]
    public async Task<IActionResult> EkHesapAc(
        string hesapNo,
        HesapAcRequest request,
        CancellationToken cancellationToken)
    {
        var anaHesap = await _context.AnaHesaplar
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.HesapNo == hesapNo && x.AktifMi && x.Musteri.AktifMi,
                cancellationToken);

        if (anaHesap is null)
        {
            return NotFound(new { mesaj = "Aktif ana hesap bulunamadı." });
        }

        var dovizKodu = request.DovizKodu.Trim().ToUpperInvariant();
        var doviz = await _context.Dovizler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == dovizKodu && x.AktifMi,
                cancellationToken);

        if (doviz is null)
        {
            return NotFound(new { mesaj = "Aktif döviz bulunamadı." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var sonEkNo = await _context.EkHesaplar
            .Where(x => x.AnaHesapId == anaHesap.Id)
            .Select(x => (int?)x.EkNo)
            .MaxAsync(cancellationToken) ?? 0;
        var olusturmaTarihi = DateTime.UtcNow;
        var ekHesap = new EkHesap
        {
            AnaHesapId = anaHesap.Id,
            EkNo = sonEkNo + 1,
            DovizId = doviz.Id,
            Bakiye = 0,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };
        _context.EkHesaplar.Add(ekHesap);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created(
            $"/api/v1/hesaplar/{hesapNo}",
            new
            {
                ekHesap.Id,
                anaHesap.HesapNo,
                ekHesap.EkNo,
                ekHesap.DovizId,
                dovizKodu = doviz.Kod,
                dovizAdi = doviz.Ad,
                ekHesap.Bakiye,
                ekHesap.AktifMi,
                ekHesap.OlusturmaTarihi
            });
    }

    [HttpGet("{hesapNo}/ek-hesaplar/{ekNo:int}/hareketler")]
    public async Task<IActionResult> HesapHareketleriniGetir(
        string hesapNo,
        int ekNo,
        CancellationToken cancellationToken)
    {
        var ekHesap = await _context.EkHesaplar
            .AsNoTracking()
            .Where(x => x.AnaHesap.HesapNo == hesapNo && x.EkNo == ekNo)
            .Select(x => new
            {
                x.Id,
                x.AnaHesap.HesapNo,
                x.EkNo,
                dovizKodu = x.Doviz.Kod,
                dovizAdi = x.Doviz.Ad,
                x.Bakiye,
                x.AktifMi
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (ekHesap is null)
        {
            return NotFound(new { mesaj = "Ana hesaba ait ek hesap bulunamadı." });
        }

        var hareketler = await _context.HesapHareketleri
            .AsNoTracking()
            .Where(x => x.HesapId == ekHesap.Id)
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

        return Ok(new { ekHesap, hareketler });
    }
}
