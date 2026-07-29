using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class DovizCevirRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir müşteri ID gönderilmelidir.")]
    public int MusteriId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir borçlu hesap Ek No gönderilmelidir.")]
    public int BorcluHesapEkNo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir alacaklı hesap Ek No gönderilmelidir.")]
    public int AlacakliHesapEkNo { get; set; }

    [Range(typeof(decimal), "0.0001", "999999999999999.9999",
        ErrorMessage = "Ödenecek döviz miktarı sıfırdan büyük olmalıdır.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal OdenecekDovizMiktari { get; set; }
}
