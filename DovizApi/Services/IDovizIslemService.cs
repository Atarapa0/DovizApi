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
    bool Bulunamadi = false,
    string? HataKodu = null,
    decimal? MevcutBakiye = null,
    decimal? IstenenMiktar = null,
    string? DovizKodu = null)
{
    public bool Basarili => Veri is not null;

    public static DovizCevirSonucu Basari(DovizCevirResponse veri) => new(veri, null);

    public static DovizCevirSonucu Hata(string mesaj, bool bulunamadi = false) =>
        new(null, mesaj, bulunamadi);

    public static DovizCevirSonucu YetersizBakiye(
        int hesapEkNo,
        decimal mevcutBakiye,
        decimal istenenMiktar,
        string dovizKodu) =>
        new(
            null,
            $"Ek No {hesapEkNo} hesabında yeterli bakiye yok. Alım gerçekleştirilemez.",
            HataKodu: "BAKIYE_YETERSIZ",
            MevcutBakiye: mevcutBakiye,
            IstenenMiktar: istenenMiktar,
            DovizKodu: dovizKodu);
}
