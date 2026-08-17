namespace DovizApi.Responses;

public sealed class ApiHataResponse
{
    public int Status { get; init; }
    public string HataKodu { get; init; } = string.Empty;
    public string Mesaj { get; init; } = string.Empty;
    public string HataId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
