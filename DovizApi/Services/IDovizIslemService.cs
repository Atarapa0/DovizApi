using DovizApi.Requests;
using DovizApi.Responses;

namespace DovizApi.Services;

public interface IDovizIslemService
{
    Task<DovizCevirSonucu> DovizCevirAsync(
        DovizCevirRequest request,
        CancellationToken cancellationToken);

    Task<DovizTersKayitSonucu> TersKayitOlusturAsync(
        string referansNo,
        string iptalNedeni,
        CancellationToken cancellationToken);
}

public sealed record DovizTersKayitSonucu(
    DovizTersKayitResponse? Veri,
    string? HataMesaji,
    bool Bulunamadi = false,
    bool Cakisma = false)
{
    public bool Basarili => Veri is not null;

    public static DovizTersKayitSonucu Basari(DovizTersKayitResponse veri) =>
        new(veri, null);

    public static DovizTersKayitSonucu Hata(
        string mesaj,
        bool bulunamadi = false,
        bool cakisma = false) =>
        new(null, mesaj, bulunamadi, cakisma);
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
