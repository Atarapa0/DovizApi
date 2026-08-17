using DovizApi.Exceptions;
using DovizApi.Requests;
using DovizApi.Responses;
using DovizApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DovizApi.Controllers;

[ApiController]
[Route("api/v1/arbitraj")]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status500InternalServerError)]
[ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status503ServiceUnavailable)]
public sealed class ArbitrajController : ControllerBase
{
    private readonly IArbitrajService _arbitrajService;

    public ArbitrajController(IArbitrajService arbitrajService)
    {
        _arbitrajService = arbitrajService;
    }

    [HttpPost("hesapla", Name = "ArbitrajHesapla")]
    [ProducesResponseType(typeof(ArbitrajHesaplaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiHataResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArbitrajHesaplaResponse>> ArbitrajHesapla(
        ArbitrajHesaplaRequest request,
        CancellationToken cancellationToken)
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
            throw new KaynakBulunamadiException(
                "ARBITRAJ_DOVIZ_BULUNAMADI",
                sonuc.HataMesaji ?? "Arbitraj için döviz bulunamadı.");
        }

        throw new GecersizIstekException(
            "ARBITRAJ_HESAPLANAMADI",
            sonuc.HataMesaji ?? "Arbitraj hesaplanamadı.");
    }
}
