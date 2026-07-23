using Microsoft.AspNetCore.Mvc;
using DovizApi.Requests;
using DovizApi.Data;
using DovizApi.Services;
using Microsoft.EntityFrameworkCore;
using System.Xml;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DovizController : ControllerBase
{
    private readonly ILogger<DovizController> _logger;
    private readonly DovizDbContext _context;
    private readonly ITcmbKurService _tcmbKurService;
    private readonly IDovizIslemService _dovizIslemService;

    public DovizController(
        ILogger<DovizController> logger,
        DovizDbContext context,
        ITcmbKurService tcmbKurService,
        IDovizIslemService dovizIslemService)
    {
        _logger = logger;
        _context = context;
        _tcmbKurService = tcmbKurService;
        _dovizIslemService = dovizIslemService;
    }

    [HttpGet("kur-oku", Name = "KurOku")]
    public async Task<IActionResult> KurOku(CancellationToken cancellationToken)
    {
        try
        {
            var kurListesi = await _tcmbKurService.KurlariGetirAsync(cancellationToken);
            return Ok(kurListesi);
        }
        catch (Exception exception) when (exception is HttpRequestException or XmlException)
        {
            _logger.LogError(exception, "TCMB kur verileri alınamadı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur verilerine şu anda ulaşılamıyor."
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "TCMB kur isteği zaman aşımına uğradı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur isteği zaman aşımına uğradı."
            });
        }
    }

    [HttpPost("doviz-al", Name = "DovizAl")]
    public async Task<IActionResult> DovizAl(
        DovizAlRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sonuc = await _dovizIslemService.DovizAlAsync(request, cancellationToken);

            if (sonuc.Basarili)
            {
                return Ok(sonuc.Veri);
            }

            if (sonuc.Bulunamadi)
            {
                return NotFound(new { mesaj = sonuc.HataMesaji });
            }

            return BadRequest(new { mesaj = sonuc.HataMesaji });
        }
        catch (Exception exception) when (exception is HttpRequestException or XmlException)
        {
            _logger.LogError(exception, "Döviz alış işlemi için TCMB kuru alınamadı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur verilerine şu anda ulaşılamıyor."
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Döviz alış işlemi sırasında TCMB isteği zaman aşımına uğradı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur isteği zaman aşımına uğradı."
            });
        }
    }

    [HttpPost("doviz-sat", Name = "DovizSat")]
    public async Task<IActionResult> DovizSat(
        DovizSatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sonuc = await _dovizIslemService.DovizSatAsync(request, cancellationToken);

            if (sonuc.Basarili)
            {
                return Ok(sonuc.Veri);
            }

            if (sonuc.Bulunamadi)
            {
                return NotFound(new { mesaj = sonuc.HataMesaji });
            }

            return BadRequest(new { mesaj = sonuc.HataMesaji });
        }
        catch (Exception exception) when (exception is HttpRequestException or XmlException)
        {
            _logger.LogError(exception, "Döviz satış işlemi için TCMB kuru alınamadı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur verilerine şu anda ulaşılamıyor."
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Döviz satış işlemi sırasında TCMB isteği zaman aşımına uğradı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur isteği zaman aşımına uğradı."
            });
        }
    }

    [HttpGet("dovizleri-getir", Name = "DovizleriGetir")]
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
    public async Task<IActionResult> DovizIslemleriGetir(CancellationToken cancellationToken)
    {
        var islemler = await _context.DovizIslemleri
            .AsNoTracking()
            .OrderByDescending(islem => islem.IslemTarihi)
            .Select(islem => new
            {
                islem.Id,
                islem.MusteriId,
                islem.DovizId,
                dovizKodu = islem.Doviz.Kod,
                dovizAdi = islem.Doviz.Ad,
                islem.IslemTuru,
                islem.DovizMiktari,
                islem.Kur,
                islem.TlTutari,
                islem.IslemTarihi
            })
            .ToListAsync(cancellationToken);

        return Ok(islemler);
    }

}
