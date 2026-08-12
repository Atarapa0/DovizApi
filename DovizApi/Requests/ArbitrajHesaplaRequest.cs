using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class ArbitrajHesaplaRequest
{
    [Required(ErrorMessage = "Başlangıç döviz kodu boş olamaz.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Başlangıç döviz kodu üç harften oluşmalıdır.")]
    public string BaslangicDovizKodu { get; set; } = string.Empty;

    [Required(ErrorMessage = "Birinci ara döviz kodu boş olamaz.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "Birinci ara döviz kodu üç harften oluşmalıdır.")]
    public string BirinciAraDovizKodu { get; set; } = string.Empty;

    [Required(ErrorMessage = "İkinci ara döviz kodu boş olamaz.")]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = "İkinci ara döviz kodu üç harften oluşmalıdır.")]
    public string IkinciAraDovizKodu { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.0001", "999999999999999.9999",
        ErrorMessage = "Başlangıç miktarı sıfırdan büyük olmalıdır.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal BaslangicMiktari { get; set; }
}
