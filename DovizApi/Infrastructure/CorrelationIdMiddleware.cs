using System.Text.RegularExpressions;

namespace DovizApi.Infrastructure;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "CorrelationId";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var gelenDeger = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = GecerliMi(gelenDeger)
            ? gelenDeger!
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Items[ItemName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await _next(context);
        }
    }

    private static bool GecerliMi(string? deger) =>
        !string.IsNullOrWhiteSpace(deger) &&
        deger.Length <= 128 &&
        CorrelationIdDeseni().IsMatch(deger);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdDeseni();
}
