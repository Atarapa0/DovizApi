using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public class DovizAlRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir müşteri ID gönderilmelidir.")]
    public int MusteriId { get; set; }

    [Required(ErrorMessage = "Döviz kodu boş olamaz.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Döviz kodu 3 karakter olmalıdır.")]
    public string DovizKodu { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999999.9999",
        ErrorMessage = "TL tutarı sıfırdan büyük olmalıdır.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal TlTutari { get; set; }
}
