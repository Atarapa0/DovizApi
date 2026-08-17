using DovizApi.Responses;
using Microsoft.AspNetCore.Mvc;

namespace DovizApi.Infrastructure;

public sealed class LoglananApiHataResult : ObjectResult
{
    private readonly HataLogService _hataLogService;
    private readonly HataKaydi _kayit;

    public LoglananApiHataResult(
        ApiHataResponse response,
        HataKaydi kayit,
        HataLogService hataLogService)
        : base(response)
    {
        _kayit = kayit;
        _hataLogService = hataLogService;
        StatusCode = response.Status;
        ContentTypes.Add("application/problem+json");
    }

    public override async Task ExecuteResultAsync(ActionContext context)
    {
        await _hataLogService.KaydetVeLoglaAsync(_kayit);
        await base.ExecuteResultAsync(context);
    }
}
