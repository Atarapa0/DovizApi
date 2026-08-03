namespace DovizApi.Responses;

public sealed class DovizIslemResponse
{
    public long Id { get; init; }
    public string ReferansNo { get; init; } = string.Empty;
    public int MusteriId { get; init; }
    public DovizIslemMusteriResponse Musteri { get; init; } = null!;
    public DovizIslemDovizResponse OdenenDoviz { get; init; } = null!;
    public DovizIslemDovizResponse AlinanDoviz { get; init; } = null!;
    public DovizIslemHesapResponse BorcluHesap { get; init; } = null!;
    public DovizIslemHesapResponse AlacakliHesap { get; init; } = null!;
    public decimal TlKarsiligi { get; init; }
    public DateTime IslemTarihi { get; init; }
    public bool TersKayitMi { get; init; }
    public bool TersKayitOlusturulduMu { get; init; }
    public string? OrijinalReferansNo { get; init; }
    public string? TersKayitReferansNo { get; init; }
    public string? IptalNedeni { get; init; }
}

public sealed class DovizIslemDetayResponse
{
    public DovizIslemResponse Islem { get; init; } = null!;
    public IReadOnlyList<HesapHareketResponse> HesapHareketleri { get; init; } = [];
}

public sealed class DovizIslemMusteriResponse
{
    public int Id { get; init; }
    public string Ad { get; init; } = string.Empty;
    public string Soyad { get; init; } = string.Empty;
    public SubeOzetResponse Sube { get; init; } = null!;
}

public sealed class DovizIslemDovizResponse
{
    public int Id { get; init; }
    public string Kod { get; init; } = string.Empty;
    public string Ad { get; init; } = string.Empty;
}

public sealed class DovizIslemHesapResponse
{
    public int HesapEkNo { get; init; }
    public string DovizKodu { get; init; } = string.Empty;
    public decimal Miktar { get; init; }
    public decimal Kur { get; init; }
}

public sealed class DovizTersKayitResponse
{
    public long IslemId { get; init; }
    public string OrijinalReferansNo { get; init; } = string.Empty;
    public string TersKayitReferansNo { get; init; } = string.Empty;
    public string IptalNedeni { get; init; } = string.Empty;
    public DateTime IslemTarihi { get; init; }
}
