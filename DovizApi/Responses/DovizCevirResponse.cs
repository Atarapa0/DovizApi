namespace DovizApi.Responses;

public sealed class DovizCevirResponse
{
    public long IslemId { get; init; }
    public Guid ReferansNo { get; init; }
    public string HesapNo { get; init; } = string.Empty;
    public int OdenenDovizId { get; init; }
    public string OdenenDovizKodu { get; init; } = string.Empty;
    public int AlinanDovizId { get; init; }
    public string AlinanDovizKodu { get; init; } = string.Empty;
    public HesapTarafiResponse BorcluHesap { get; init; } = null!;
    public HesapTarafiResponse AlacakliHesap { get; init; } = null!;
    public decimal TlKarsiligi { get; init; }
    public DateTime IslemTarihi { get; init; }
}

public sealed class HesapTarafiResponse
{
    public int EkNo { get; init; }
    public string DovizKodu { get; init; } = string.Empty;
    public decimal DovizMiktari { get; init; }
    public decimal UygulananKur { get; init; }
    public decimal YeniBakiye { get; init; }
}
