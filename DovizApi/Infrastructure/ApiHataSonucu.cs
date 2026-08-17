using DovizApi.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DovizApi.Infrastructure;

public static class ApiHataSonucu
{
    public static IActionResult Olustur(
        HttpContext context,
        int status,
        string hataKodu,
        string mesaj,
        Exception exception,
        bool kritik = false)
    {
        var service = context.RequestServices.GetRequiredService<HataLogService>();
        var kayit = service.KayitOlustur(context, exception, status, hataKodu, mesaj, kritik);
        var response = ResponseOlustur(kayit);
        return new LoglananApiHataResult(response, kayit, service);
    }

    public static ApiHataResponse ResponseOlustur(HataKaydi kayit) => new()
    {
        Status = kayit.HttpStatus,
        HataKodu = kayit.HataKodu,
        Mesaj = kayit.Mesaj,
        HataId = kayit.HataId,
        CorrelationId = kayit.CorrelationId,
        Timestamp = kayit.Timestamp
    };
}
