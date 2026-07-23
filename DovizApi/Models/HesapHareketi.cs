namespace DovizApi.Models;

public sealed class HesapHareketi
{
    public long Id { get; set; }
    public long DovizIslemId { get; set; }
    public long HesapId { get; set; }
    public string HareketTuru { get; set; } = string.Empty;
    public decimal DovizMiktari { get; set; }
    public decimal TlKarsiligi { get; set; }
    public DateTime IslemTarihi { get; set; }

    public DovizIslemi DovizIslemi { get; set; } = null!;
    public MusteriHesabi Hesap { get; set; } = null!;
}
