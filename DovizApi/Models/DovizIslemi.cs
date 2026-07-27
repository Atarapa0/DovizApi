namespace DovizApi.Models;

public sealed class DovizIslemi
{
    public long Id { get; set; }
    public Guid ReferansNo { get; set; }
    public long BorcluHesapId { get; set; }
    public long AlacakliHesapId { get; set; }
    public int OdenenDovizId { get; set; }
    public int AlinanDovizId { get; set; }
    public decimal OdenenDovizMiktari { get; set; }
    public decimal AlinanDovizMiktari { get; set; }
    public decimal OdenenDovizKuru { get; set; }
    public decimal AlinanDovizKuru { get; set; }
    public decimal TlKarsiligi { get; set; }
    public DateTime IslemTarihi { get; set; }

    public EkHesap BorcluHesap { get; set; } = null!;
    public EkHesap AlacakliHesap { get; set; } = null!;
    public Doviz OdenenDoviz { get; set; } = null!;
    public Doviz AlinanDoviz { get; set; } = null!;
    public ICollection<HesapHareketi> HesapHareketleri { get; set; } = [];
}
