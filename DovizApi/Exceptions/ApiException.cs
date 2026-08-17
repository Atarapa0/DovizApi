namespace DovizApi.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(
        int statusCode,
        string hataKodu,
        string mesaj,
        bool kritik = false,
        Exception? innerException = null)
        : base(mesaj, innerException)
    {
        StatusCode = statusCode;
        HataKodu = hataKodu;
        GuvenliMesaj = mesaj;
        Kritik = kritik;
    }

    public int StatusCode { get; }
    public string HataKodu { get; }
    public string GuvenliMesaj { get; }
    public bool Kritik { get; }
}

public sealed class GecersizIstekException : ApiException
{
    public GecersizIstekException(string hataKodu, string mesaj)
        : base(StatusCodes.Status400BadRequest, hataKodu, mesaj)
    {
    }
}

public sealed class KaynakBulunamadiException : ApiException
{
    public KaynakBulunamadiException(string hataKodu, string mesaj)
        : base(StatusCodes.Status404NotFound, hataKodu, mesaj)
    {
    }
}

public sealed class IsKuraliException : ApiException
{
    public IsKuraliException(string hataKodu, string mesaj, bool kritik = false)
        : base(StatusCodes.Status409Conflict, hataKodu, mesaj, kritik)
    {
    }
}

public sealed class BagimlilikKullanilamiyorException : ApiException
{
    public BagimlilikKullanilamiyorException(
        string hataKodu = "SERVIS_KULLANILAMIYOR",
        string mesaj = "İlgili servise şu anda ulaşılamıyor.",
        Exception? innerException = null)
        : base(StatusCodes.Status503ServiceUnavailable, hataKodu, mesaj, innerException: innerException)
    {
    }
}
