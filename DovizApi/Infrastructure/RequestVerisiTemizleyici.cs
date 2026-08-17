using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DovizApi.Options;
using Microsoft.Extensions.Options;

namespace DovizApi.Infrastructure;

public sealed partial class RequestVerisiTemizleyici
{
    public const string TemizBodyItemKey = "TemizRequestBody";
    private const string MaskeliDeger = "***MASKELENDI***";
    private static readonly HashSet<string> HassasAlanlar = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "parola", "token", "authorization", "accessToken", "refreshToken",
        "connectionString", "kartNumarasi", "tckn", "vergiNo"
    };

    private readonly HataLoglamaOptions _options;

    public RequestVerisiTemizleyici(IOptions<HataLoglamaOptions> options)
    {
        _options = options.Value;
    }

    public async Task BodyHazirlaAsync(HttpContext context)
    {
        if (context.Request.ContentLength == 0 ||
            !JsonIcerikMi(context.Request.ContentType))
        {
            return;
        }

        try
        {
            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            var buffer = new char[Math.Max(1, _options.RequestBodyMaksimumKarakter + 1)];
            var okunan = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
            context.Request.Body.Position = 0;
            var body = new string(buffer, 0, Math.Min(okunan, _options.RequestBodyMaksimumKarakter));
            context.Items[TemizBodyItemKey] = JsonTemizle(body, okunan > _options.RequestBodyMaksimumKarakter);
        }
        catch
        {
            context.Request.Body.Position = 0;
            context.Items[TemizBodyItemKey] = "[REQUEST_BODY_OKUNAMADI]";
        }
    }

    public string? QueryStringTemizle(HttpRequest request)
    {
        if (request.Query.Count == 0)
        {
            return null;
        }

        var parcalar = request.Query.SelectMany(x => x.Value.Select(value =>
            $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(HassasAlanlar.Contains(x.Key) ? MaskeliDeger : value ?? string.Empty)}"));
        return Sinirla("?" + string.Join("&", parcalar), _options.QueryStringMaksimumKarakter);
    }

    public static string GuvenliMetin(string? deger, int maksimum = 8000)
    {
        if (string.IsNullOrWhiteSpace(deger))
        {
            return string.Empty;
        }

        var temiz = HassasMetinDeseni().Replace(deger, match =>
            $"{match.Groups[1].Value}={MaskeliDeger}");
        return Sinirla(temiz, maksimum);
    }

    private static string JsonTemizle(string body, bool kesildi)
    {
        try
        {
            var node = JsonNode.Parse(body);
            Maskele(node);
            var sonuc = node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? string.Empty;
            return kesildi ? Sinirla(sonuc, body.Length) + "...[KESILDI]" : sonuc;
        }
        catch (JsonException)
        {
            return Sinirla(GuvenliMetin(body), body.Length) + (kesildi ? "...[KESILDI]" : string.Empty);
        }
    }

    private static void Maskele(JsonNode? node)
    {
        if (node is JsonObject nesne)
        {
            foreach (var alan in nesne.ToList())
            {
                if (HassasAlanlar.Contains(alan.Key))
                {
                    nesne[alan.Key] = MaskeliDeger;
                }
                else
                {
                    Maskele(alan.Value);
                }
            }
        }
        else if (node is JsonArray dizi)
        {
            foreach (var eleman in dizi)
            {
                Maskele(eleman);
            }
        }
    }

    private static bool JsonIcerikMi(string? contentType) =>
        contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static string Sinirla(string deger, int maksimum) =>
        deger.Length <= maksimum ? deger : deger[..maksimum];

    [GeneratedRegex("(?i)(password|parola|token|authorization|accessToken|refreshToken|connectionString|kartNumarasi|tckn|vergiNo)\\s*[=:]\\s*[\\\"']?[^;\\\"'\\s,}]+")]
    private static partial Regex HassasMetinDeseni();
}
