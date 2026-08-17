using System.Xml;
using DovizApi.Exceptions;
using DovizApi.Responses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DovizApi.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly HataLogService _hataLogService;

    public GlobalExceptionHandler(HataLogService hataLogService)
    {
        _hataLogService = hataLogService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var esleme = Esle(exception, httpContext.RequestAborted.IsCancellationRequested);
        var kayit = _hataLogService.KayitOlustur(
            httpContext,
            exception,
            esleme.Status,
            esleme.HataKodu,
            esleme.Mesaj,
            esleme.Kritik);

        await _hataLogService.KaydetVeLoglaAsync(kayit);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = esleme.Status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(
            new ApiHataResponse
            {
                Status = esleme.Status,
                HataKodu = esleme.HataKodu,
                Mesaj = esleme.Mesaj,
                HataId = kayit.HataId,
                CorrelationId = kayit.CorrelationId,
                Timestamp = kayit.Timestamp
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }

    private static HataEsleme Esle(Exception exception, bool istemciIptalEtti)
    {
        if (exception is ApiException apiException)
        {
            return new HataEsleme(
                apiException.StatusCode,
                apiException.HataKodu,
                apiException.GuvenliMesaj,
                apiException.Kritik);
        }

        if (exception is HttpRequestException or XmlException ||
            exception is TaskCanceledException && !istemciIptalEtti)
        {
            return new HataEsleme(
                StatusCodes.Status503ServiceUnavailable,
                "SERVIS_KULLANILAMIYOR",
                "İlgili servise şu anda ulaşılamıyor.");
        }

        var sqlException = SqlExceptionBul(exception);
        if (sqlException?.Number is 2601 or 2627)
        {
            return new HataEsleme(
                StatusCodes.Status409Conflict,
                "KAYIT_CAKISMASI",
                "Aynı bilgilerle çakışan bir kayıt bulunuyor.");
        }

        if (VeritabaniBaglantiHatasiMi(exception))
        {
            return new HataEsleme(
                StatusCodes.Status503ServiceUnavailable,
                "VERITABANI_KULLANILAMIYOR",
                "İlgili servise şu anda ulaşılamıyor.");
        }

        if (exception is DbUpdateException || sqlException is not null)
        {
            return new HataEsleme(
                StatusCodes.Status500InternalServerError,
                "VERITABANI_ISLEM_HATASI",
                "İşlem sırasında beklenmeyen bir hata oluştu.",
                Kritik: true);
        }

        return new HataEsleme(
            StatusCodes.Status500InternalServerError,
            "BEKLENMEYEN_HATA",
            "İşlem sırasında beklenmeyen bir hata oluştu.");
    }

    private static bool VeritabaniBaglantiHatasiMi(Exception exception)
    {
        var sqlException = SqlExceptionBul(exception);
        return sqlException is not null &&
               (sqlException.IsTransient || sqlException.Number is
                   -2 or 20 or 53 or 64 or 233 or 258 or 1205 or 4060 or
                   10053 or 10054 or 10060 or 10928 or 10929 or 40197 or
                   40501 or 40613 or 49918 or 49919 or 49920);
    }

    private static SqlException? SqlExceptionBul(Exception exception)
    {
        for (Exception? mevcut = exception; mevcut is not null; mevcut = mevcut.InnerException)
        {
            if (mevcut is SqlException sqlException)
            {
                return sqlException;
            }
        }

        return null;
    }

    private sealed record HataEsleme(
        int Status,
        string HataKodu,
        string Mesaj,
        bool Kritik = false);
}
