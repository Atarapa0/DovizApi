using DovizApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/subeler")]
public sealed class SubelerController : ControllerBase
{
    private readonly DovizDbContext _context;

    public SubelerController(DovizDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> SubeleriGetir(CancellationToken cancellationToken)
    {
        var subeler = await _context.Subeler
            .AsNoTracking()
            .OrderBy(sube => sube.Kod)
            .Select(sube => new
            {
                sube.Id,
                sube.Kod,
                sube.Ad,
                sube.AktifMi,
                musteriSayisi = sube.Musteriler.Count,
                sube.OlusturmaTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(subeler);
    }
}
