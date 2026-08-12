using DovizApi.Requests;
using DovizApi.Responses;

namespace DovizApi.Services;

public interface IArbitrajService
{
    Task<ArbitrajHesaplamaSonucu> ArbitrajHesaplaAsync(
        ArbitrajHesaplaRequest request,
        CancellationToken cancellationToken);
}

public sealed record ArbitrajHesaplamaSonucu(
    ArbitrajHesaplaResponse? Veri,
    string? HataMesaji,
    bool Bulunamadi = false)
{
    public bool Basarili => Veri is not null;

    public static ArbitrajHesaplamaSonucu Basari(ArbitrajHesaplaResponse veri) =>
        new(veri, null);

    public static ArbitrajHesaplamaSonucu Hata(
        string mesaj,
        bool bulunamadi = false) =>
        new(null, mesaj, bulunamadi);
}
