namespace DovizApi.Responses;

public sealed class MusteriListeResponse
{
    public int Id { get; init; }
    public string Ad { get; init; } = string.Empty;
    public string Soyad { get; init; } = string.Empty;
    public bool AktifMi { get; init; }
    public SubeOzetResponse Sube { get; init; } = null!;
    public int HesapSayisi { get; init; }
    public DateTime OlusturmaTarihi { get; init; }
    public DateTime GuncellemeTarihi { get; init; }
}

public sealed class MusteriAramaResponse
{
    public int Id { get; init; }
    public string Ad { get; init; } = string.Empty;
    public string Soyad { get; init; } = string.Empty;
    public bool AktifMi { get; init; }
    public SubeOzetResponse Sube { get; init; } = null!;
    public int HesapSayisi { get; init; }
}

public sealed class SubeOzetResponse
{
    public int Id { get; init; }
    public string Kod { get; init; } = string.Empty;
    public string Ad { get; init; } = string.Empty;
}

public sealed class MusteriHesapHareketleriResponse
{
    public int MusteriId { get; init; }
    public string Ad { get; init; } = string.Empty;
    public string Soyad { get; init; } = string.Empty;
    public IReadOnlyList<HesapHareketleriResponse> Hesaplar { get; init; } = [];
}

public sealed class HesapHareketleriResponse
{
    public int HesapEkNo { get; init; }
    public int DovizId { get; init; }
    public string DovizKodu { get; init; } = string.Empty;
    public string DovizAdi { get; init; } = string.Empty;
    public decimal Bakiye { get; init; }
    public bool AktifMi { get; init; }
    public IReadOnlyList<HesapHareketResponse> Hareketler { get; init; } = [];
}

public sealed class HesapHareketResponse
{
    public long Id { get; init; }
    public long DovizIslemId { get; init; }
    public string ReferansNo { get; init; } = string.Empty;
    public string HareketTuru { get; init; } = string.Empty;
    public decimal DovizMiktari { get; init; }
    public decimal TlKarsiligi { get; init; }
    public DateTime IslemTarihi { get; init; }
}
