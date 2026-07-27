namespace DovizApi.Models;

public sealed class EkHesap
{
    public long Id { get; set; }
    public long AnaHesapId { get; set; }
    public int EkNo { get; set; }
    public int DovizId { get; set; }
    public decimal Bakiye { get; set; }
    public bool AktifMi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public DateTime GuncellemeTarihi { get; set; }

    public AnaHesap AnaHesap { get; set; } = null!;
    public Doviz Doviz { get; set; } = null!;
    public ICollection<DovizIslemi> BorcluOlduguIslemler { get; set; } = [];
    public ICollection<DovizIslemi> AlacakliOlduguIslemler { get; set; } = [];
    public ICollection<HesapHareketi> Hareketler { get; set; } = [];
}
