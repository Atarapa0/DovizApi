using System.Data;
using System.Security.Cryptography;
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
        var subeKodu = request.SubeKodu.Trim().ToUpperInvariant();
        var sube = await _context.Subeler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == subeKodu && x.AktifMi,
                cancellationToken);

        if (sube is null)
        {
            return NotFound(new { mesaj = "Aktif şube bulunamadı." });
        }

        var tryDoviz = await _context.Dovizler
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Kod == "TRY" && x.AktifMi,
                cancellationToken);

        if (tryDoviz is null)
        {
            return BadRequest(new { mesaj = "Aktif TRY para birimi bulunamadı." });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var hesapNo = await BenzersizHesapNoUretAsync(cancellationToken);
        var olusturmaTarihi = DateTime.UtcNow;
        var musteri = new Musteri
        {
            Ad = request.Ad.Trim(),
            Soyad = request.Soyad.Trim(),
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi
        };
        var anaHesap = new AnaHesap
        {
            HesapNo = hesapNo,
            Musteri = musteri,
            SubeId = sube.Id,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };
        var tryEkHesabi = new EkHesap
        {
            AnaHesap = anaHesap,
            EkNo = 1,
            DovizId = tryDoviz.Id,
            Bakiye = request.BaslangicTryBakiyesi,
            AktifMi = true,
            OlusturmaTarihi = olusturmaTarihi,
            GuncellemeTarihi = olusturmaTarihi
        };

        _context.EkHesaplar.Add(tryEkHesabi);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Created(
            $"/api/v1/hesaplar/{hesapNo}",
            new
            {
                musteri = new
                {
                    musteri.Id,
                    musteri.Ad,
                    musteri.Soyad,
                    musteri.AktifMi
                },
                anaHesap = new
                {
                    anaHesap.Id,
                    anaHesap.HesapNo,
                    anaHesap.AktifMi,
                    sube = new { sube.Id, sube.Kod, sube.Ad },
                    anaHesap.OlusturmaTarihi
                },
                ilkEkHesap = new
                {
                    tryEkHesabi.Id,
                    tryEkHesabi.EkNo,
                    dovizKodu = tryDoviz.Kod,
                    dovizAdi = tryDoviz.Ad,
                    tryEkHesabi.Bakiye
                }
            });
    }

    [HttpGet]
    public async Task<IActionResult> MusterileriGetir(CancellationToken cancellationToken)
    {
        var musteriler = await _context.Musteriler
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Ad,
                x.Soyad,
                x.AktifMi,
                x.OlusturmaTarihi,
                anaHesaplar = x.AnaHesaplar
                    .OrderBy(hesap => hesap.Id)
                    .Select(hesap => new
                    {
                        hesap.Id,
                        hesap.HesapNo,
                        subeKodu = hesap.Sube.Kod,
                        hesap.AktifMi
                    })
            })
            .ToListAsync(cancellationToken);

        return Ok(musteriler);
    }

    private async Task<string> BenzersizHesapNoUretAsync(CancellationToken cancellationToken)
    {
        const int maksimumDeneme = 10;

        for (var deneme = 0; deneme < maksimumDeneme; deneme++)
        {
            var ilkRakam = RandomNumberGenerator.GetInt32(1, 10);
            var kalanRakamlar = RandomNumberGenerator.GetInt32(0, 1_000_000_000);
            var hesapNo = $"{ilkRakam}{kalanRakamlar:D9}";

            var kullaniliyor = await _context.AnaHesaplar
                .AsNoTracking()
                .AnyAsync(x => x.HesapNo == hesapNo, cancellationToken);

            if (!kullaniliyor)
            {
                return hesapNo;
            }
        }

        throw new InvalidOperationException("Benzersiz hesap numarası üretilemedi.");
    }
}
