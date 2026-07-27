namespace DovizApi.Models;

public sealed class AnaHesap
{
    public long Id { get; set; }
    public string HesapNo { get; set; } = string.Empty;
    public int MusteriId { get; set; }
    public int SubeId { get; set; }
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime GuncellemeTarihi { get; set; }

    public Musteri Musteri { get; set; } = null!;
    public Sube Sube { get; set; } = null!;
    public ICollection<EkHesap> EkHesaplar { get; set; } = [];
}
