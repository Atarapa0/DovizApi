using System.Globalization;
using System.Xml.Linq;

namespace DovizApi.Services;

public sealed class TcmbKurService : ITcmbKurService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TcmbKurService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<TcmbKurListesi> KurlariGetirAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("Tcmb");
        var xml = await httpClient.GetStringAsync("kurlar/today.xml", cancellationToken);
        var belge = XDocument.Parse(xml);

        var kurlar = belge.Descendants("Currency")
            .Select(currency => new TcmbKur(
                currency.Attribute("CurrencyCode")?.Value ?? string.Empty,
                currency.Element("Isim")?.Value ?? string.Empty,
                ParseBirim(currency.Element("Unit")?.Value),
                ParseKur(currency.Element("ForexBuying")?.Value),
                ParseKur(currency.Element("ForexSelling")?.Value)))
            .Where(kur => !string.IsNullOrWhiteSpace(kur.Kod))
            .ToArray();

        return new TcmbKurListesi(
            belge.Root?.Attribute("Date")?.Value,
            kurlar);
    }

    private static short ParseBirim(string? deger)
    {
        return short.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out var birim)
            ? birim
            : (short)1;
    }

    private static decimal? ParseKur(string? deger)
    {
        return decimal.TryParse(deger, NumberStyles.Number, CultureInfo.InvariantCulture, out var kur)
            ? kur
            : null;
    }
}
