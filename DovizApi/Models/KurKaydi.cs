namespace DovizApi.Models;

public class KurKaydi
{
    public long Id { get; set; }
    public int DovizId { get; set; }
    public DateOnly KurTarihi { get; set; }
    public short Birim { get; set; }
    public decimal AlisKuru { get; set; }
    public decimal SatisKuru { get; set; }
    public DateTime OlusturmaTarihi { get; set; }

    public Doviz Doviz { get; set; } = null!;
}
