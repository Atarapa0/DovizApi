using System.Xml;
using DovizApi.Requests;
using DovizApi.Responses;
using DovizApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/arbitraj")]
public sealed class ArbitrajController : ControllerBase
{
    private readonly ILogger<ArbitrajController> _logger;
    private readonly IArbitrajService _arbitrajService;

    public ArbitrajController(
        ILogger<ArbitrajController> logger,
        IArbitrajService arbitrajService)
    {
        _logger = logger;
        _arbitrajService = arbitrajService;
    }

    [HttpPost("hesapla", Name = "ArbitrajHesapla")]
    public async Task<ActionResult<ArbitrajHesaplaResponse>> ArbitrajHesapla(
        ArbitrajHesaplaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var sonuc = await _arbitrajService.ArbitrajHesaplaAsync(
                request,
                cancellationToken);

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
            _logger.LogError(exception, "Arbitraj hesabı için TCMB kurları alınamadı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur verilerine şu anda ulaşılamıyor."
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Arbitraj hesabı sırasında TCMB isteği zaman aşımına uğradı.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                mesaj = "TCMB kur isteği zaman aşımına uğradı."
            });
        }
    }
}
