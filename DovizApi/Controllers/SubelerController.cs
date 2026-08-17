using DovizApi.Data;
using DovizApi.Exceptions;
using DovizApi.Models;
using DovizApi.Requests;
using DovizApi.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/subeler")]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status503ServiceUnavailable)]
public sealed class SubelerController : ControllerBase
{
    private readonly DovizDbContext _context;

    public SubelerController(DovizDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubeResponse>>> SubeleriGetir(
        CancellationToken cancellationToken)
    {
        var subeler = await _context.Subeler
            .AsNoTracking()
            .OrderBy(sube => sube.Kod)
            .Select(sube => new SubeResponse
            {
                Id = sube.Id,
                Kod = sube.Kod,
                Ad = sube.Ad,
                AktifMi = sube.AktifMi,
                MusteriSayisi = sube.Musteriler.Count,
                OlusturmaTarihi = sube.OlusturmaTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(subeler);
    }

    [HttpGet("{subeKodu}")]
    [ProducesResponseType(typeof(SubeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubeResponse>> SubeDetayiniGetir(
        string subeKodu,
        CancellationToken cancellationToken)
    {
        var kod = subeKodu.Trim();
        if (kod.Length != 4 || !kod.All(char.IsDigit))
        {
            throw new GecersizIstekException(
                "SUBE_KODU_GECERSIZ",
                "Şube kodu dört rakamdan oluşmalıdır.");
        }

        var sube = await _context.Subeler
            .AsNoTracking()
            .Where(x => x.Kod == kod)
            .Select(x => new SubeResponse
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad,
                AktifMi = x.AktifMi,
                MusteriSayisi = x.Musteriler.Count,
                OlusturmaTarihi = x.OlusturmaTarihi
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (sube is null)
        {
            throw new KaynakBulunamadiException("SUBE_BULUNAMADI", "Şube bulunamadı.");
        }

        return Ok(sube);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SubeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubeResponse>> SubeOlustur(
        SubeOlusturRequest request,
        CancellationToken cancellationToken)
    {
        var kod = request.Kod.Trim();
        var ad = request.Ad.Trim();

        if (string.IsNullOrWhiteSpace(ad))
        {
            throw new GecersizIstekException("SUBE_ADI_GECERSIZ", "Şube adı boş olamaz.");
        }

        var kodKullaniliyor = await _context.Subeler
            .AsNoTracking()
            .AnyAsync(sube => sube.Kod == kod, cancellationToken);

        if (kodKullaniliyor)
        {
            throw new IsKuraliException(
                "SUBE_KODU_CAKISMASI",
                $"{kod} kodlu şube zaten mevcut.");
        }

        var sube = new Sube
        {
            Kod = kod,
            Ad = ad,
            AktifMi = true,
            OlusturmaTarihi = DateTime.UtcNow
        };

        _context.Subeler.Add(sube);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new SubeResponse
        {
            Id = sube.Id,
            Kod = sube.Kod,
            Ad = sube.Ad,
            AktifMi = sube.AktifMi,
            MusteriSayisi = 0,
            OlusturmaTarihi = sube.OlusturmaTarihi
        };

        return CreatedAtAction(
            nameof(SubeleriGetir),
            routeValues: null,
            value: response);
    }
}
