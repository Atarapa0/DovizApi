namespace DovizApi.Options;

public sealed class HataLoglamaOptions
{
    public const string SectionName = "HataLoglama";

    public bool SqlEtkin { get; set; } = true;
    public bool SqlBeklenenHatalariKaydet { get; set; }
    public string[] SqlKritik409HataKodlari { get; set; } = [];
    public int RequestBodyMaksimumKarakter { get; set; } = 4096;
    public int QueryStringMaksimumKarakter { get; set; } = 2048;
    public int SqlYazmaZamanAsimiSaniye { get; set; } = 3;
}
