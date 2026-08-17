using System.Globalization;
using System.Xml.Linq;
using DovizApi.Exceptions;

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
        XDocument belge;
        try
        {
            var httpClient = _httpClientFactory.CreateClient("Tcmb");
            var xml = await httpClient.GetStringAsync("kurlar/today.xml", cancellationToken);
            belge = XDocument.Parse(xml);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Xml.XmlException)
        {
            throw new BagimlilikKullanilamiyorException(
                "TCMB_KULLANILAMIYOR",
                "TCMB kur verilerine şu anda ulaşılamıyor.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BagimlilikKullanilamiyorException(
                "TCMB_ZAMAN_ASIMI",
                "TCMB kur isteği zaman aşımına uğradı.",
                exception);
        }

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
