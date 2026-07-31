using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class MusteriOlusturRequest
{
    [Required(ErrorMessage = "Ad boş olamaz.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    public string Ad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad boş olamaz.")]
    [StringLength(100, ErrorMessage = "Soyad en fazla 100 karakter olabilir.")]
    public string Soyad { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şube kodu boş olamaz.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Şube kodu dört rakamdan oluşmalıdır.")]
    public string SubeKodu { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999999.9999",
        ErrorMessage = "Başlangıç TRY bakiyesi negatif olamaz.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal BaslangicTryBakiyesi { get; set; }
}
