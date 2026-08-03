using System.ComponentModel.DataAnnotations;

namespace DovizApi.Requests;

public sealed class IslemIptalRequest
{
    [Required(ErrorMessage = "İptal nedeni boş olamaz.")]
    [StringLength(500, ErrorMessage = "İptal nedeni en fazla 500 karakter olabilir.")]
    public string IptalNedeni { get; set; } = string.Empty;
}
