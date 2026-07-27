namespace DovizApi.Models;

public sealed class Sube
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public ICollection<AnaHesap> AnaHesaplar { get; set; } = [];
}
