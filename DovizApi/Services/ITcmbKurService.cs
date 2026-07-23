namespace DovizApi.Services;

public interface ITcmbKurService
{
    Task<TcmbKurListesi> KurlariGetirAsync(CancellationToken cancellationToken);
}

public sealed record TcmbKurListesi(string? Tarih, IReadOnlyList<TcmbKur> Kurlar);

public sealed record TcmbKur(
    string Kod,
    string Isim,
    short Birim,
    decimal? DovizAlis,
    decimal? DovizSatis);
