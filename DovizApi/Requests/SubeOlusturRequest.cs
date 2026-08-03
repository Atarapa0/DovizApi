using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class SubeOlusturRequest
{
    [Required(ErrorMessage = "Şube kodu boş olamaz.")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Şube kodu dört rakamdan oluşmalıdır.")]
    public string Kod { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şube adı boş olamaz.")]
    [StringLength(100, ErrorMessage = "Şube adı en fazla 100 karakter olabilir.")]
    public string Ad { get; set; } = string.Empty;
}
