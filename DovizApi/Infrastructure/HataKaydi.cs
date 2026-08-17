namespace DovizApi.Infrastructure;

public sealed record HataKaydi(
    string HataId,
    string CorrelationId,
    DateTime Timestamp,
    int HttpStatus,
    string HataKodu,
    string Mesaj,
    string Seviye,
    bool Kritik,
    Exception Exception,
    string Endpoint,
    string HttpMethod,
    string? QueryString,
    string? RequestBody,
    int? MusteriId,
    string? KullaniciId,
    string? SubeKodu);
