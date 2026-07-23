namespace DovizApi.Models;

public class Musteri
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Soyad { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public ICollection<MusteriHesabi> Hesaplar { get; set; } = [];
    public ICollection<DovizIslemi> DovizIslemleri { get; set; } = [];
}
