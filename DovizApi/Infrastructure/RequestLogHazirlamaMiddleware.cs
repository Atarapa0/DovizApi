namespace DovizApi.Infrastructure;

public sealed class RequestLogHazirlamaMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLogHazirlamaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, RequestVerisiTemizleyici temizleyici)
    {
        await temizleyici.BodyHazirlaAsync(context);
        await _next(context);
    }
}
