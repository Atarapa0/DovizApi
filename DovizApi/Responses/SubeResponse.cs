namespace DovizApi.Responses;

public sealed class SubeResponse
{
    public int Id { get; init; }
    public string Kod { get; init; } = string.Empty;
    public string Ad { get; init; } = string.Empty;
    public bool AktifMi { get; init; }
    public int MusteriSayisi { get; init; }
    public DateTime OlusturmaTarihi { get; init; }
}
