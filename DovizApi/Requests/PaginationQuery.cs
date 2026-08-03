using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public class PaginationQuery
{
    [Range(1, int.MaxValue, ErrorMessage = "Sayfa numarası en az 1 olmalıdır.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 20;
}

public sealed class MusteriListeQuery : PaginationQuery
{
    [StringLength(100, ErrorMessage = "Arama metni en fazla 100 karakter olabilir.")]
    public string? Arama { get; set; }

    [RegularExpression(@"^\d{4}$", ErrorMessage = "Şube kodu dört rakamdan oluşmalıdır.")]
    public string? SubeKodu { get; set; }
}

public sealed class DovizIslemListeQuery : PaginationQuery
{
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Şube kodu dört rakamdan oluşmalıdır.")]
    public string? SubeKodu { get; set; }
}

public sealed class MusteriAraQuery
{
    [Required(ErrorMessage = "Arama metni boş olamaz.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "Arama metni 1 ile 100 karakter arasında olmalıdır.")]
    public string Q { get; set; } = string.Empty;

    [Range(1, 50, ErrorMessage = "Limit 1 ile 50 arasında olmalıdır.")]
    public int Limit { get; set; } = 10;
}
