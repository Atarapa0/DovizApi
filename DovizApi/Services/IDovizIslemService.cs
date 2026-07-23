using DovizApi.Requests;
using DovizApi.Responses;

namespace DovizApi.Services;

public interface IDovizIslemService
{
    Task<DovizCevirSonucu> DovizCevirAsync(
        DovizCevirRequest request,
        CancellationToken cancellationToken);
}

public sealed record DovizCevirSonucu(
    DovizCevirResponse? Veri,
    string? HataMesaji,
    bool Bulunamadi = false)
{
    public bool Basarili => Veri is not null;

    public static DovizCevirSonucu Basari(DovizCevirResponse veri) => new(veri, null);

    public static DovizCevirSonucu Hata(string mesaj, bool bulunamadi = false) =>
        new(null, mesaj, bulunamadi);
}
