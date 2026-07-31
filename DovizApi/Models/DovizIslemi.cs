namespace DovizApi.Models;

public sealed class DovizIslemi
{
    public long Id { get; set; }
    public string ReferansNo { get; set; } = string.Empty;
    public int MusteriId { get; set; }
    public int BorcluHesapEkNo { get; set; }
    public int AlacakliHesapEkNo { get; set; }
    public int OdenenDovizId { get; set; }
    public int AlinanDovizId { get; set; }
    public decimal OdenenDovizMiktari { get; set; }
    public decimal AlinanDovizMiktari { get; set; }
    public decimal OdenenDovizKuru { get; set; }
    public decimal AlinanDovizKuru { get; set; }
    public decimal TlKarsiligi { get; set; }
    public DateTime IslemTarihi { get; set; }

    public MusteriHesabi BorcluHesap { get; set; } = null!;
    public MusteriHesabi AlacakliHesap { get; set; } = null!;
    public Doviz OdenenDoviz { get; set; } = null!;
    public Doviz AlinanDoviz { get; set; } = null!;
    public ICollection<HesapHareketi> HesapHareketleri { get; set; } = [];
}
