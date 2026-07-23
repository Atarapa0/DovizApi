namespace DovizApi.Models;

public sealed class MusteriHesabi
{
    public long Id { get; set; }
    public int MusteriId { get; set; }
    public int EkNo { get; set; }
    public int DovizId { get; set; }
    public decimal Bakiye { get; set; }
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime GuncellemeTarihi { get; set; }

    public Musteri Musteri { get; set; } = null!;
    public Doviz Doviz { get; set; } = null!;
    public ICollection<DovizIslemi> BorcluOlduguIslemler { get; set; } = [];
    public ICollection<DovizIslemi> AlacakliOlduguIslemler { get; set; } = [];
    public ICollection<HesapHareketi> Hareketler { get; set; } = [];
}
