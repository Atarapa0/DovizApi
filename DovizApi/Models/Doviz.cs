namespace DovizApi.Models;

public class Doviz
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public short Birim { get; set; }
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public ICollection<MusteriHesabi> MusteriHesaplari { get; set; } = [];
    public ICollection<KurKaydi> KurKayitlari { get; set; } = [];
    public ICollection<DovizIslemi> OdenenOlduguIslemler { get; set; } = [];
    public ICollection<DovizIslemi> AlinanOlduguIslemler { get; set; } = [];
}
