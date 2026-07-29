namespace DovizApi.Models;

public class Musteri
{
    public int Id { get; set; }
    public int SubeId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime GuncellemeTarihi { get; set; }

    public Sube Sube { get; set; } = null!;
    public ICollection<MusteriHesabi> Hesaplar { get; set; } = [];
}
