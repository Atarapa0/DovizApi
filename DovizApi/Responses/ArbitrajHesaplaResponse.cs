namespace DovizApi.Responses;

public sealed class ArbitrajHesaplaResponse
{
    public string? KurTarihi { get; init; }
    public string BaslangicDovizKodu { get; init; } = string.Empty;
    public decimal BaslangicMiktari { get; init; }
    public IReadOnlyList<ArbitrajAdimiResponse> Adimlar { get; init; } = [];
    public decimal SonMiktar { get; init; }
    public decimal KarZararTutari { get; init; }
    public decimal KarZararOrani { get; init; }
    public bool ArbitrajFirsatiVarMi { get; init; }
    public string Aciklama { get; init; } = string.Empty;
}

public sealed class ArbitrajAdimiResponse
{
    public int Sira { get; init; }
    public string KaynakDovizKodu { get; init; } = string.Empty;
    public string HedefDovizKodu { get; init; } = string.Empty;
    public decimal GirisMiktari { get; init; }
    public decimal KaynakAlisKuru { get; init; }
    public decimal HedefSatisKuru { get; init; }
    public decimal TlKarsiligi { get; init; }
    public decimal CikisMiktari { get; init; }
}
