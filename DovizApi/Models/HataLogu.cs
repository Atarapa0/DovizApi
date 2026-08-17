namespace DovizApi.Models;

public sealed class HataLogu
{
    public long Id { get; set; }
    public string HataId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Tarih { get; set; }
    public string Seviye { get; set; } = string.Empty;
    public int HttpStatus { get; set; }
    public string HataKodu { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string? Detay { get; set; }
    public string? StackTrace { get; set; }
    public string? ExceptionTipi { get; set; }
    public string? Endpoint { get; set; }
    public string? HttpMethod { get; set; }
    public string? QueryString { get; set; }
    public int? MusteriId { get; set; }
    public string? KullaniciId { get; set; }
    public string? SubeKodu { get; set; }
    public string Ortam { get; set; } = string.Empty;
    public string Kaynak { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
}
