using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class DovizCevirRequest
{
    [Required(ErrorMessage = "Hesap numarası boş olamaz.")]
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "Hesap numarası 10 rakamdan oluşmalıdır.")]
    public string HesapNo { get; set; } = string.Empty;

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
