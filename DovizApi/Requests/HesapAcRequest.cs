using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class HesapAcRequest
{
    [Required(ErrorMessage = "Döviz kodu boş olamaz.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Döviz kodu 3 karakter olmalıdır.")]
    public string DovizKodu { get; set; } = string.Empty;
}
