using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using DovizApi.Data;
using DovizApi.Models;
using DovizApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DovizApi.Infrastructure;

public sealed class HataLogService
{
    private readonly IDbContextFactory<DovizDbContext> _contextFactory;
    private readonly RequestVerisiTemizleyici _temizleyici;
    private readonly HataLoglamaOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HataLogService> _logger;

    public HataLogService(
        IDbContextFactory<DovizDbContext> contextFactory,
        RequestVerisiTemizleyici temizleyici,
        IOptions<HataLoglamaOptions> options,
        IWebHostEnvironment environment,
        ILogger<HataLogService> logger)
    {
        _contextFactory = contextFactory;
        _temizleyici = temizleyici;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public HataKaydi KayitOlustur(
        HttpContext context,
        Exception exception,
        int status,
        string hataKodu,
        string mesaj,
        bool kritik)
    {
        var body = context.Items.TryGetValue(RequestVerisiTemizleyici.TemizBodyItemKey, out var bodyValue)
            ? bodyValue as string
            : null;
        var musteriId = DegerBul(context, body, "musteriId", "MusteriId", "musteriId");
        var subeKodu = MetinBul(context, body, "subeKodu", "SubeKodu", "subeKodu");

        return new HataKaydi(
            HataIdUret(),
            context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? context.TraceIdentifier,
            DateTime.UtcNow,
            status,
            hataKodu,
            mesaj,
            SeviyeBelirle(status, kritik),
            kritik,
            exception,
            context.Request.Path.Value ?? string.Empty,
            context.Request.Method,
            _temizleyici.QueryStringTemizle(context.Request),
            body,
            musteriId,
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity?.Name,
            subeKodu);
    }

    public async Task KaydetVeLoglaAsync(HataKaydi kayit)
    {
        StructuredLogYaz(kayit);

        if (!SqlKaydedilmeli(kayit))
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(Math.Clamp(_options.SqlYazmaZamanAsimiSaniye, 1, 30)));
            await using var context = await _contextFactory.CreateDbContextAsync(timeout.Token);
            context.HataLoglari.Add(new HataLogu
            {
                HataId = kayit.HataId,
                CorrelationId = kayit.CorrelationId,
                Tarih = kayit.Timestamp,
                Seviye = kayit.Seviye,
                HttpStatus = kayit.HttpStatus,
                HataKodu = kayit.HataKodu,
                Mesaj = Sinirla(kayit.Mesaj, 1000)!,
                Detay = RequestVerisiTemizleyici.GuvenliMetin(kayit.Exception.InnerException?.Message),
                StackTrace = RequestVerisiTemizleyici.GuvenliMetin(kayit.Exception.StackTrace, 32000),
                ExceptionTipi = Sinirla(kayit.Exception.GetType().FullName, 500),
                Endpoint = Sinirla(kayit.Endpoint, 2048),
                HttpMethod = Sinirla(kayit.HttpMethod, 16),
                QueryString = Sinirla(kayit.QueryString, 2048),
                MusteriId = kayit.MusteriId,
                KullaniciId = Sinirla(kayit.KullaniciId, 256),
                SubeKodu = Sinirla(kayit.SubeKodu, 20),
                Ortam = Sinirla(_environment.EnvironmentName, 50)!,
                Kaynak = "DovizApi",
                RequestBody = kayit.RequestBody
            });
            await context.SaveChangesAsync(timeout.Token);
        }
        catch (Exception logException)
        {
            // Hata loglama hatası asıl exception'ı ve müşterinin cevabını kesinlikle gizlemez.
            _logger.LogError(
                "SQL hata kaydı yazılamadı. HataId: {HataId}, CorrelationId: {CorrelationId}, LoglamaHatasi: {LoglamaHatasi}",
                kayit.HataId,
                kayit.CorrelationId,
                RequestVerisiTemizleyici.GuvenliMetin(logException.Message));
        }
    }

    private void StructuredLogYaz(HataKaydi kayit)
    {
        var exceptionOzeti = $"{kayit.Exception.GetType().Name}: {RequestVerisiTemizleyici.GuvenliMetin(kayit.Exception.Message)}";
        var logSeviyesi = kayit.Kritik
            ? LogLevel.Critical
            : kayit.HttpStatus >= 500 ? LogLevel.Error
            : kayit.HttpStatus == 409 ? LogLevel.Warning
            : LogLevel.Information;

        _logger.Log(
            logSeviyesi,
            "API hatası. HataId: {HataId}, CorrelationId: {CorrelationId}, HttpStatus: {HttpStatus}, HataKodu: {HataKodu}, RequestPath: {RequestPath}, HttpMethod: {HttpMethod}, MusteriId: {MusteriId}, KullaniciId: {KullaniciId}, SubeKodu: {SubeKodu}, Environment: {Environment}, ApplicationName: {ApplicationName}, Exception: {Exception}",
            kayit.HataId,
            kayit.CorrelationId,
            kayit.HttpStatus,
            kayit.HataKodu,
            kayit.Endpoint,
            kayit.HttpMethod,
            kayit.MusteriId,
            kayit.KullaniciId,
            kayit.SubeKodu,
            _environment.EnvironmentName,
            "DovizApi",
            exceptionOzeti);
    }

    private bool SqlKaydedilmeli(HataKaydi kayit) =>
        _options.SqlEtkin &&
        (kayit.HttpStatus is 500 or 503 ||
         _options.SqlBeklenenHatalariKaydet ||
         (kayit.HttpStatus == 409 &&
          (_options.SqlKritik409HataKodlari.Contains(kayit.HataKodu, StringComparer.OrdinalIgnoreCase) || kayit.Kritik)));

    private static string HataIdUret() =>
        $"ERR-{Guid.NewGuid():N}"[..16].ToUpperInvariant();

    private static string SeviyeBelirle(int status, bool kritik) =>
        kritik ? "Critical" : status >= 500 ? "Error" : status == 409 ? "Warning" : "Information";

    private static int? DegerBul(HttpContext context, string? body, string routeKey, string queryKey, string bodyKey)
    {
        if (int.TryParse(context.Request.RouteValues[routeKey]?.ToString(), out var routeValue) ||
            int.TryParse(context.Request.Query[queryKey].FirstOrDefault(), out routeValue))
        {
            return routeValue;
        }

        return int.TryParse(JsonAlanBul(body, bodyKey), out var bodyValue) ? bodyValue : null;
    }

    private static string? MetinBul(HttpContext context, string? body, string routeKey, string queryKey, string bodyKey) =>
        context.Request.RouteValues[routeKey]?.ToString() ??
        context.Request.Query[queryKey].FirstOrDefault() ??
        JsonAlanBul(body, bodyKey);

    private static string? JsonAlanBul(string? body, string alan)
    {
        try
        {
            return JsonNode.Parse(body ?? string.Empty)?[alan]?.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Sinirla(string? deger, int maksimum) =>
        string.IsNullOrEmpty(deger) || deger.Length <= maksimum ? deger : deger[..maksimum];
}
