# Döviz API Proje Anlatımı

> Güncel hesap modeli: `Şube -> Müşteri -> Müşteri Hesapları`. Ayrı ana hesap ve 10 haneli hesap numarası yoktur. Her hesabın kimliği `(MusteriId, HesapEkNo)` birleşimidir ve ek numaralar her müşteri için 5001'den başlar.

Bu doküman projedeki dosyaların, sınıfların ve önemli teknik tercihlerin ne amaçla kullanıldığını açıklamak için hazırlanmıştır. Amaç yalnızca kodun ne yaptığını değil, neden bu şekilde tasarlandığını da anlatabilmektir.

## Projenin genel çalışma akışı

Örneğin müşteri 1.000 TRY ödeyerek EUR almak istediğinde akış şöyledir:

```text
HTTP isteği
    ↓
DovizController
    ↓
IDovizIslemService
    ↓
DovizIslemService
    ├── Müşteri ve hesapları kontrol eder
    ├── TCMB'den güncel kuru alır
    ├── Tutarları hesaplar
    ├── Bakiye yeterliliğini kontrol eder
    └── Transaction içinde veritabanına kaydeder
```

Projede sorumluluklar şu şekilde ayrılmıştır:

- `Controllers`: HTTP isteklerini karşılar ve HTTP cevaplarını oluşturur.
- `Services`: Döviz dönüşümü ve TCMB kuru alma gibi iş kurallarını yürütür.
- `Data`: Entity Framework ve MSSQL bağlantısını yönetir.
- `Models`: Veritabanı tablolarının C# karşılıklarını içerir.
- `Requests`: API'ye dışarıdan gönderilebilecek verileri tanımlar.
- `Responses`: API'nin dışarıya döndüreceği verileri tanımlar.
- `Database`: Boş veritabanında elle çalıştırılan tek kurulum scriptini içerir.

Bu ayrımın amacı her sınıfın tek ve anlaşılır bir sorumluluğa sahip olmasıdır.

---

# Interface neden kullanıldı?

## IDovizIslemService

`IDovizIslemService`, döviz işlemi servisinin sözleşmesidir.

Interface bir işlemin nasıl yapılacağını değil, hangi işlemlerin sunulacağını belirtir. Örneğin interface şu anlama gelir:

> Döviz işlemi yapan bir servis `DovizCevirAsync` metodunu sağlamak zorundadır.

Controller gerçek sınıfa doğrudan bağlı değildir:

```csharp
private readonly IDovizIslemService _dovizIslemService;
```

Doğrudan şu şekilde bir bağımlılık kurulmamıştır:

```csharp
private readonly DovizIslemService _dovizIslemService;
```

Bunun nedenleri:

- Controller, servisin işlemi nasıl gerçekleştirdiğini bilmez.
- Controller somut bir sınıfa sıkı şekilde bağlanmaz.
- Unit test sırasında gerçek servis yerine sahte veya mock servis verilebilir.
- İleride servis implementasyonu değiştirilebilir.
- Nesnelerin oluşturulmasını Dependency Injection sistemi yönetir.

Örneğin test sırasında şu şekilde sahte bir servis yazılabilir:

```csharp
public class SahteDovizIslemService : IDovizIslemService
{
    public Task<DovizCevirSonucu> DovizCevirAsync(...)
    {
        // Veritabanına ve TCMB'ye bağlanmadan test sonucu döndürür.
    }
}
```

Sorulursa verilebilecek cevap:

> Controller'ı gerçek servis implementasyonuna doğrudan bağımlı bırakmamak için interface kullandım. Bu sayede bağımlılığı azalttım, servisi testlerde mocklayabilirim ve ileride implementasyon değişirse controller'ı değiştirmem gerekmez.

Küçük bir projede yalnızca tek implementasyon varsa interface teknik olarak zorunlu değildir. Bu projede katmanları ayırmak ve test edilebilirliği artırmak amacıyla tercih edilmiştir.

## ITcmbKurService

`ITcmbKurService`, TCMB kur servisinin sözleşmesidir.

Kullanılma nedenleri:

- Controller'ın gerçek XML okuma sınıfına doğrudan bağlanmaması
- Döviz işlem servisinin TCMB implementasyonuna sıkı bağlı olmaması
- Testlerde sabit kur döndüren sahte servis kullanılabilmesi
- İleride farklı kur sağlayıcısına geçilebilmesi

Örneğin ileride aşağıdaki gibi başka bir implementasyon yazılabilir:

```csharp
public class AlternatifKurService : ITcmbKurService
{
    // Kurları farklı sağlayıcıdan getirir.
}
```

Controller ve döviz işlem servisi interface'e bağlı olduğu için bu değişiklikte onların kodunun değiştirilmesi gerekmez.

---

# Program.cs

`Program.cs`, uygulamanın başlangıç ve yapılandırma dosyasıdır.

## WebApplicationBuilder

```csharp
var builder = WebApplication.CreateBuilder(args);
```

ASP.NET Core uygulamasının servis, ayar ve loglama altyapısını hazırlar.

## appsettings.Local.json yüklenmesi

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}
```

Gerçek veritabanı bağlantı bilgilerinin ana ayar dosyasına ve Git'e gönderilmemesi için local ayar dosyası kullanılır.

- Yalnızca Development ortamında yüklenir.
- `optional: true`, dosya bulunmadığında bu satırda hata verilmemesini sağlar.
- `reloadOnChange: true`, ayar değiştiğinde tekrar okunmasını sağlar.

Savunma cümlesi:

> Hassas connection string bilgisini ana ayar dosyasından ayırmak ve Git'e göndermemek için Development ortamına özel local configuration kullandım.

## AddControllers

```csharp
builder.Services.AddControllers();
```

Controller tabanlı API sistemini açar. Bu kayıt olmadan `[ApiController]`, `[HttpGet]` ve `[HttpPost]` gibi controller özellikleri çalışmaz.

## Connection string kontrolü

```csharp
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(...);
```

Veritabanı bağlantısı bulunamazsa uygulamanın eksik ayarla çalışmasını engeller.

Bu yaklaşıma fail-fast denir: Zorunlu bir ayar eksikse uygulama ileride anlaşılmaz hatalar vermek yerine başlangıçta açıkça hata verir.

## DbContext kaydı

```csharp
builder.Services.AddDbContext<DovizDbContext>(options =>
    options.UseSqlServer(connectionString));
```

Entity Framework Core'a:

- Kullanılacak DbContext sınıfını
- Veritabanı sağlayıcısının MSSQL olduğunu
- Kullanılacak connection string'i

tanıtır.

`AddDbContext` varsayılan olarak scoped çalışır. Her HTTP isteği için ayrı DbContext oluşturulur.

## Named HttpClient

```csharp
builder.Services.AddHttpClient("Tcmb", client =>
{
    client.BaseAddress = new Uri("https://www.tcmb.gov.tr/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

TCMB istekleri için isimlendirilmiş bir HTTP istemcisi oluşturur.

Her istekte `new HttpClient()` yapılmamasının nedenleri:

- HttpClient yaşam döngüsünü `IHttpClientFactory` yönetir.
- Gereksiz bağlantı ve socket tüketimi önlenir.
- TCMB ana adresi tek yerde tutulur.
- Timeout tek yerde belirlenir.
- İleride retry ve loglama politikaları merkezi olarak eklenebilir.

Savunma cümlesi:

> HttpClient yaşam döngüsünü doğru yönetmek ve TCMB bağlantı ayarlarını merkezi tutmak için `IHttpClientFactory` ile named client kullandım.

## Servislerin Dependency Injection kaydı

```csharp
builder.Services.AddScoped<ITcmbKurService, TcmbKurService>();
builder.Services.AddScoped<IDovizIslemService, DovizIslemService>();
```

Bu kayıtların anlamı:

```text
Bir sınıf ITcmbKurService isterse TcmbKurService ver.
Bir sınıf IDovizIslemService isterse DovizIslemService ver.
```

Controller içinde elle `new DovizIslemService(...)` yapılmaz. Nesneleri DI container oluşturur ve bağımlılıklarını bağlar.

## Swagger

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

Controller endpointlerinden OpenAPI dokümanı üretir.

```csharp
app.UseSwagger();
app.UseSwaggerUI();
```

Swagger JSON dokümanını ve kullanıcı arayüzünü açar. Mevcut projede yalnızca Development ortamında aktiftir.

## Middleware sırası

```csharp
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- `UseHttpsRedirection`: HTTP isteklerini HTTPS'e yönlendirir.
- `UseAuthorization`: Yetkilendirme middleware'ini çalıştırır.
- `MapControllers`: Controller route'larını uygulamaya bağlar.
- `Run`: Uygulamayı başlatır.

Şu an `UseAuthorization` bulunmasına rağmen gerçek kullanıcı girişi ve authentication sistemi henüz uygulanmamıştır.

---

# Controllers/DovizApi.cs - DovizController

Bu controller TCMB kuru, döviz listesi ve döviz dönüşümüyle ilgili HTTP isteklerini karşılar.

Dosyanın adı `DovizApi.cs`, sınıfın adı `DovizController` şeklindedir. Çalışmasına engel değildir fakat dosya adının standart olarak `DovizController.cs` olması daha uygundur.

## ApiController

```csharp
[ApiController]
```

Sınıfın bir API controller'ı olduğunu belirtir.

Sağladığı özellikler:

- Request body otomatik olarak C# nesnesine çevrilir.
- Validation hataları otomatik `400 Bad Request` döndürür.
- Parametre binding işlemleri düzenlenir.

## Route

```csharp
[Route("api/v1")]
```

Controller endpointlerinin başlangıç adresini belirler.

`v1`, API versiyonunu ifade eder. İleride geriye dönük uyumluluğu bozacak değişiklikler yapılırsa `/api/v2` oluşturulabilir.

## Constructor injection

Controller ihtiyaç duyduğu bağımlılıkları constructor üzerinden alır:

- `ILogger<DovizController>`: Hataları ve önemli durumları loglamak için
- `DovizDbContext`: Basit veritabanı sorguları için
- `ITcmbKurService`: TCMB kurlarını almak için
- `IDovizIslemService`: Döviz dönüşümünü gerçekleştirmek için

Bağımlılıkların sınıf içinde oluşturulmaması sınıfı daha test edilebilir yapar ve neye ihtiyaç duyduğunu açıkça gösterir.

## GET /api/v1/kur-oku

TCMB servisinden güncel kurları alır.

Controller XML parse etmez. XML indirme ve parse etme işlemi `TcmbKurService` sınıfının sorumluluğudur.

Controller'ın yaptığı işler:

- Kur servisini çağırmak
- Başarılıysa `200 OK` döndürmek
- TCMB veya XML hatası varsa loglamak
- Dış servis kullanılamıyorsa `503 Service Unavailable` döndürmek

`503` kullanılmasının nedeni kendi API'mizin çalışmasına rağmen bağlı olunan TCMB servisinin kullanılamamasıdır.

## POST /api/v1/doviz-cevir

Döviz dönüşüm isteğini alır ve `IDovizIslemService` servisine gönderir.

Controller bakiye düşürmez ve kur hesabı yapmaz. Bunlar HTTP katmanına değil iş katmanına ait kurallardır.

Servis sonucu HTTP cevabına dönüştürülür:

- Başarılıysa `200 OK`
- Müşteri veya hesap bulunamadıysa `404 Not Found`
- Bakiye veya iş kuralı hatasıysa `400 Bad Request`
- TCMB kullanılamıyorsa `503 Service Unavailable`

## POST /api/v1/arbitraj/hesapla

Başlangıç dövizi, iki ara döviz ve başlangıç miktarı üzerinden teorik üçlü arbitraj simülasyonu yapar.

```text
Başlangıç → Birinci ara döviz → İkinci ara döviz → Başlangıç
```

Her dönüşüm adımında kaynak döviz TCMB alış kurundan TL'ye çevrilir, ardından hedef döviz TCMB satış kurundan alınır. TRY kuru `1` kabul edilir. Ara sonuçlar dört ondalık basamağa `ToZero` yönünde yuvarlanır.

Endpoint yalnızca hesaplama yapar; müşteri bakiyesi, döviz işlemi veya hesap hareketi oluşturmaz. Bu nedenle veritabanı transaction'ı ve yeni bir SQL tablosu kullanmaz.

Başarılı cevapta üç dönüşüm adımı, son miktar, kâr/zarar tutarı, kâr/zarar oranı ve teorik fırsat durumu bulunur. Tek kur kaynağı TCMB olduğu için bu sonuç farklı piyasalardaki gerçek arbitraj fırsatını değil, TCMB alış-satış kurlarına göre teorik simülasyonu temsil eder.

## GET /api/v1/dovizleri-getir

Veritabanındaki aktif dövizleri getirir.

```csharp
.AsNoTracking()
```

Veriler yalnızca okunduğu için Entity Framework change tracking kapatılır. Bu, gereksiz bellek ve işlem maliyetini azaltır.

```csharp
.Select(...)
```

Tablonun tamamı yerine istemciye gerekli alanları seçer. Gereksiz kolonların SQL'den alınmasını ve entity'nin doğrudan dışarı açılmasını önler.

## GET /api/v1/doviz-islemleri-getir

Gerçekleşmiş döviz işlemlerini en yeniden en eskiye getirir.

Borçlu ve alacaklı hesapların döviz bilgilerine navigation property'ler üzerinden ulaşılır. Entity Framework, `Select` içerisindeki navigation kullanımlarını gerekli SQL JOIN ifadelerine çevirir.

Bu nedenle projection kullanılan sorguda ayrıca `Include` yazılması gerekmez.

---

# Controllers/MusterilerController.cs

`MusterilerController` müşteri oluşturma/listeleme ile müşteriye bağlı döviz hesabı işlemlerini yönetir.

Müşteri işlemlerinin döviz controller'ından ayrılmasının nedeni farklı sorumlulukları ayrı sınıflarda tutmaktır.

## POST /api/v1/musteriler

Yeni müşteri ve `HesapEkNo = 5001` TRY hesabını tek transaction içinde oluşturur. İstekte alınan `subeKodu` doğrudan müşteriye bağlanır.

İlk olarak aktif TRY para biriminin ID'si veritabanından bulunur. TRY ID'si doğrudan `1` yazılmamıştır; çünkü farklı veritabanlarında aynı dövizin ID'si farklı olabilir.

Müşteri ve TRY hesabı aynı transaction içinde oluşturulur. Bunun amacı şu hatalı durumun oluşmasını engellemektir:

```text
Müşteri kaydedildi.
TRY hesabı oluşturulamadı.
Sistemde hesapsız müşteri kaldı.
```

İşlemlerden biri başarısız olursa ikisi de rollback edilir.

Transaction için `IsolationLevel.Serializable` kullanılır. Bu seviye aynı anda çalışan işlemler arasındaki veri çakışmalarını azaltır. Dezavantajı daha fazla kilit oluşturabilmesidir; bu projede veri tutarlılığı öncelikli tutulmuştur.

Başarılı sonuçta `CreatedAtAction` kullanılır. Yeni kaynak oluşturulduğu için REST yaklaşımına uygun olarak `201 Created` döndürülür ve oluşturulan kaynağa ulaşılabilecek adres belirtilir.

## POST /api/v1/musteriler/{musteriId}/hesaplar

Müşteriye yeni döviz hesabı açar.

Kontroller:

1. Müşteri var ve aktif mi?
2. Müşteri ID geçerli mi?
3. Döviz kodu var mı?
4. Döviz aktif mi?

Döviz kodu şu şekilde düzenlenir:

```csharp
request.DovizKodu.Trim().ToUpperInvariant()
```

Kullanıcı `" eur "` gönderirse değer `"EUR"` hâline getirilir.

Yeni hesap ek numarası müşterinin mevcut en yüksek numarasının bir fazlası olarak hesaplanır. İlk numara 5001'dir; sonraki hesaplar 5002, 5003 şeklinde ilerler.

## GET /api/v1/musteriler

Bütün müşterileri salt okunur olarak getirir. `AsNoTracking` ve yalnızca gerekli alanları alan `Select` projection kullanılır.

## GET /api/v1/musteriler/{musteriId}/hesaplar

Müşteri, bağlı şube ve döviz hesaplarını hesap ek numarasına göre sıralanmış olarak getirir.

Müşteri ve hesap sorgularının ayrı yapılmasının nedenleri:

- Müşteri yoksa açıkça `404 Not Found` döndürmek
- Müşteri varsa ve henüz hesabı yoksa müşteriyi boş hesap listesiyle göstermek

## GET /api/v1/musteriler/{musteriId}/hesaplar/{hesapEkNo}/hareketler

Belirli müşterinin birleşik `(MusteriId, HesapEkNo)` anahtarlı hesabını bulur ve hareketlerini en yeniden en eskiye getirir.

Hesap hareketleri, hesabın ekstresi gibi düşünülebilir.

---

# Services/ITcmbKurService.cs

TCMB kur servisinin interface'ini ve kur verisini taşıyan record tiplerini içerir.

## Record neden kullanıldı?

TCMB'den gelen kur sonucu davranış içermeyen, yalnızca veri taşıyan bir yapıdır.

Record kullanılmasının nedenleri:

- Veri taşıma nesneleri için kısa ve okunabilir tanım sağlar.
- Değer bazlı karşılaştırma yapar.
- Immutable kullanıma uygundur.
- Kur nesnesinin sonradan değiştirilmesini zorlaştırır.

Savunma cümlesi:

> TCMB sonucu davranış içermeyen ve yalnızca veri taşıyan bir yapı olduğu için record kullandım.

---

# Services/TcmbKurService.cs

TCMB'den gerçek XML'i indirir ve C# nesnelerine dönüştürür.

## IHttpClientFactory kullanımı

Program.cs'de tanımlanan `Tcmb` isimli HttpClient alınır:

```csharp
var httpClient = _httpClientFactory.CreateClient("Tcmb");
```

Ardından göreceli adres kullanılır:

```csharp
var xml = await httpClient.GetStringAsync(
    "kurlar/today.xml",
    cancellationToken);
```

Base address ile birleşince gerçek adres şöyledir:

```text
https://www.tcmb.gov.tr/kurlar/today.xml
```

## CancellationToken

İstemci HTTP isteğini iptal ederse veya bağlantıyı kapatırsa devam eden TCMB ve veritabanı işlemlerinin de durdurulabilmesi için kullanılır.

Bu sayede artık sonucu kullanmayacak bir istek için sunucu kaynak tüketmeye devam etmez.

## XML parse işlemi

```csharp
var belge = XDocument.Parse(xml);
```

XML metnini sorgulanabilir bir XML dokümanına çevirir.

```csharp
belge.Descendants("Currency")
```

Bütün `Currency` elementlerini bulur.

Her para birimi için şu alanlar okunur:

- `CurrencyCode`
- `Isim`
- `Unit`
- `ForexBuying`
- `ForexSelling`

## InvariantCulture

TCMB ondalık sayılarda nokta kullanır:

```text
40.1234
```

Türkçe kültürde ondalık ayracı virgüldür. Sunucunun bölgesel ayarına göre yanlış parse oluşmaması için `CultureInfo.InvariantCulture` kullanılır.

Finansal değerlerin yanlış okunmasını engelleyen önemli bir ayrıntıdır.

## Nullable kur değerleri

TCMB XML'indeki bazı kur alanları boş olabilir. Bu alanlar `decimal?` olarak tutulur.

Boş bir kur değerini zorla sıfıra dönüştürmek finansal açıdan yanlış olacağı için veri yokluğu `null` ile ifade edilir.

## Mevcut compiler uyarısı

```csharp
belge.Root.Attribute("Date").Value
```

satırında XML kökü veya `Date` attribute'u teorik olarak null olabileceği için `CS8602` uyarısı oluşmaktadır. Normal TCMB XML'inde bu alan vardır ancak beklenmeyen XML'e karşı daha güvenli hâle getirilmesi gereken bir noktadır.

---

# Services/IDovizIslemService.cs

Döviz dönüşüm servisinin sözleşmesini ve servis sonuç modelini içerir.

## Servis neden IActionResult döndürmüyor?

`IActionResult`, HTTP ve controller katmanına aittir. İş katmanının `200`, `400` veya `404` gibi HTTP detaylarına bağımlı olması istenmez.

Servis bunun yerine şu tür iş sonuçları üretir:

- İşlem başarılı
- Kayıt bulunamadı
- İş kuralı hatası oluştu

Controller bu sonucu uygun HTTP cevabına çevirir.

Savunma cümlesi:

> Business katmanını HTTP'ye bağımlı bırakmamak için servis sonucunu ayrı bir sonuç modeliyle döndürdüm.

---

# Services/DovizIslemService.cs

Projenin temel finansal iş kuralları bu sınıftadır.

Controller'dan ayrı olmasının nedeni döviz dönüşümünün HTTP'den bağımsız bir iş kuralı olmasıdır. Aynı işlem ileride controller, mobil uygulama, mesaj kuyruğu veya zamanlanmış görev tarafından kullanılabilir.

## 1. Aynı hesap kontrolü

Borçlu ve alacaklı ek numarası aynı olamaz. Bir hesabın kendi kendisine döviz çevirmesi anlamsızdır.

## 2. Müşteri ve hesap kontrolü

Belirtilen müşterinin aktif olup olmadığı ve gönderilen hesapların o müşteriye ait olup olmadığı kontrol edilir.

Bu kontrol başka müşterinin hesabıyla işlem yapılmasını engeller. Tam güvenlik için ileride authentication ve authorization da eklenmelidir.

## 3. Aynı döviz kontrolü

İki hesap aynı döviz cinsindeyse işlem reddedilir.

Örneğin EUR hesabından başka EUR hesabına para aktarmak döviz dönüşümü değil, virman işlemidir ve ayrı bir iş akışı olmalıdır.

## 4. TCMB kurunun tek seferde alınması

Ödenen ve alınan döviz için iki ayrı TCMB isteği yapılmaz. Kurlar bir kere alınır ve işlemin iki tarafında aynı kur listesi kullanılır.

Faydaları:

- Gereksiz HTTP istekleri engellenir.
- İki farklı zamanda alınmış kurlar kullanılmaz.
- İşlem kendi içinde tutarlı olur.

## 5. Alış ve satış kuru seçimi

Ödenecek döviz için TCMB alış kuru kullanılır. Çünkü sistem müşterinin verdiği dövizi müşteriden alır.

Alınacak döviz için TCMB satış kuru kullanılır. Çünkü sistem alınacak dövizi müşteriye satar.

Örnek:

```text
Müşteri EUR veriyor → Sistem EUR'yu alış kurundan alır.
Müşteri USD alıyor  → Sistem USD'yi satış kurundan verir.
```

## 6. TRY kuru

TL temel karşılık olarak kullanıldığı için:

```text
1 TRY = 1 TL
```

kabul edilir. TCMB XML'inde TRY için ayrıca kur bulunmaz.

## 7. TCMB birim hesabı

JPY gibi bazı para birimlerinin kuru 100 birim üzerinden verilebilir.

Gerçek tek birim fiyatı şu şekilde hesaplanır:

```text
Birim fiyat = TCMB kuru / TCMB birimi
```

Bu hesap yapılmazsa 100 JPY'nin kuru yanlışlıkla 1 JPY kuru gibi kullanılır.

## 8. Çapraz döviz hesabı

Örneğin EUR ile USD alınacaksa hesap TL üzerinden yapılır:

```text
EUR → TL karşılığı → USD
```

Önce:

```text
TL karşılığı = Ödenen EUR × EUR alış kuru
```

Sonra:

```text
Alınan USD = TL karşılığı / USD satış kuru
```

Bu yaklaşım bütün para birimlerinin ortak TL karşılığı üzerinden çevrilmesini sağlar.

## 9. Decimal kullanımı

Finansal değerlerde `float` veya `double` ikili sayı sisteminden dolayı hassasiyet hataları oluşturabilir.

`decimal`, ondalık ve finansal hesaplar için daha uygun olduğu için tutar ve kur alanlarında kullanılmıştır.

## 10. Yuvarlama

Döviz miktarları 4, kur değerleri 6 ondalık basamak hassasiyetinde tutulur.

`ToZero` yönünde yuvarlama sistemin gerçekte olmayan küsuratı müşteriye vermesini engeller.

## 11. TCMB isteğinin transaction'dan önce yapılması

İnternet isteği yavaşlayabilir. TCMB isteği SQL transaction'ı içinde yapılırsa veritabanı kilitleri gereksiz yere uzun süre açık kalabilir.

Bu nedenle:

1. Önce TCMB kuru alınır.
2. Hesaplama yapılır.
3. Ardından kısa süreli SQL transaction'ı açılır.

## 12. Bakiye kontrolü

Borçlu, yani ödeyen hesabın yeterli bakiyesi olup olmadığı kontrol edilir. Bakiye yetersizse hiçbir bakiye veya işlem kaydı değiştirilmez.

## 13. Borçlu ve alacaklı hesap

Projede kullanılan iş kuralı şöyledir:

```text
Borçlu hesap   → Ödenecek döviz hesabı → Bakiye azalır
Alacaklı hesap → Alınacak döviz hesabı → Bakiye artar
```

Örnek olarak 1.000 TRY ile EUR alındığında:

```text
TRY hesabı → BORC   → TRY bakiyesi azalır
EUR hesabı → ALACAK → EUR bakiyesi artar
```

## 14. DovizIslemi ve HesapHareketi ayrımı

`DovizIslemi`, işlemin ana belgesidir:

- İşlemi hangi müşteri yaptı?
- Referans numarası nedir?
- Borçlu ve alacaklı hesap hangileridir?
- Hangi kurlar kullanıldı?
- TL karşılığı nedir?
- İşlem ne zaman yapıldı?

`HesapHareketi` ise işlemin tek bir hesaba yansımasıdır:

```text
TRY hesabına BORC hareketi
EUR hesabına ALACAK hareketi
```

Bu ayrım hesap ekstresi ve işlem raporu üretmeyi kolaylaştırır.

## 15. Tek transaction kullanılması

Bakiye değişiklikleri, ana işlem kaydı ve iki hesap hareketi aynı transaction içinde kaydedilir.

Şu hatalı durumların oluşması engellenir:

```text
TRY hesabı azaldı ama EUR hesabı artmadı.
DovizIslemi oluştu ama HesapHareketi oluşmadı.
BORC hareketi oluştu ama ALACAK hareketi oluşmadı.
```

Her adım başarılıysa commit yapılır. Herhangi bir hata olursa bütün değişiklikler rollback edilir.

---

# Request dosyaları neden var?

Request sınıfları API kullanıcısının hangi alanları gönderebileceğini belirler.

Veritabanı entity'lerini doğrudan request olarak kullanmamanın nedenleri:

- Kullanıcının sistem tarafından belirlenmesi gereken alanları göndermesini engellemek
- Over-posting riskini azaltmak
- API sözleşmesini veritabanı yapısından ayırmak
- Validation kurallarını açıkça tanımlamak

## Requests/DovizCevirRequest.cs

Döviz dönüşümünde kullanıcının gönderebileceği alanları içerir:

- `MusteriId`
- `BorcluHesapEkNo`
- `AlacakliHesapEkNo`
- `OdenecekDovizMiktari`

Kullanıcı şu alanları belirleyemez:

- Referans numarası
- Uygulanacak kur
- Alınacak döviz miktarı
- TL karşılığı
- İşlem tarihi

Bu alanları sistem hesaplar ve üretir.

Müşteri ID, hesap ek numaraları ve tutarlar `Range` ile doğrulanır.

`[ApiController]` sayesinde validation başarısızsa action çalışmadan otomatik `400 Bad Request` döner.

## Requests/HesapAcRequest.cs

Yeni hesap açılması için gerekli döviz kodunu taşır.

Döviz kodu:

- Zorunludur.
- Tam üç karakter olmalıdır.

## Requests/MusteriOlusturRequest.cs

Yeni müşteri için gönderilebilecek alanları içerir:

- Ad
- Soyad
- Şube kodu
- Başlangıç TRY bakiyesi

İstemcinin müşteri ID'si, aktiflik durumu veya oluşturulma tarihini belirlemesine izin verilmez.

---

# Responses/DovizCevirResponse.cs

Başarılı döviz dönüşümünde istemciye gönderilecek veriyi tanımlar.

Response modelini entity'den ayrı tutmanın nedenleri:

- Veritabanı yapısını doğrudan dışarı açmamak
- Gereksiz alanları göndermemek
- Navigation döngülerini önlemek
- API cevabını veritabanı şemasından bağımsız tutmak
- İstemciye daha anlaşılır alan isimleri sunmak

`HesapTarafiResponse`, hem borçlu hem alacaklı hesap için ortak yapıyı temsil eder:

- Ek numarası
- Döviz kodu
- Döviz miktarı
- Uygulanan kur
- İşlem sonrası yeni bakiye

Alanlarda `init` kullanılması nesne oluşturulduktan sonra değerlerin yanlışlıkla değiştirilmesini azaltır.

---

# Data/DovizDbContext.cs

Entity Framework Core ile MSSQL arasındaki ana köprüdür.

## DbSet alanları

Her `DbSet`, sorgulanabilir bir veritabanı tablosunu temsil eder:

- `Dovizler`
- `Subeler`
- `Musteriler`
- `MusteriHesaplari`
- `KurKayitlari`
- `DovizIslemleri`
- `HesapHareketleri`

Örneğin `_context.Musteriler` üzerinden yazılan LINQ sorgusu Entity Framework tarafından SQL sorgusuna çevrilir.

## Fluent API

`OnModelCreating` içinde modellerin veritabanına nasıl eşleneceği belirlenir:

- Tablo isimleri
- Primary key alanları
- Metin uzunlukları
- Decimal hassasiyetleri
- Varsayılan değerler
- Unique indexler
- Foreign key ilişkileri
- Silme davranışları
- Check constraintler

Bu kuralların merkezi olarak DbContext içinde tutulması veritabanı eşlemesini daha görünür ve düzenli yapar.

## Decimal hassasiyeti

Para miktarlarında genellikle:

```text
decimal(19,4)
```

kur değerlerinde:

```text
decimal(19,6)
```

kullanılır.

Kur değerleri hesaplamada daha hassas olması gerektiği için daha fazla ondalık basamakla tutulur.

## Unique ek numarası

```csharp
entity.HasKey(x => new { x.MusteriId, x.HesapEkNo });
```

Aynı müşteride aynı hesap ek numarasının iki defa açılmasını engeller.

Farklı müşterilerin aynı hesap ek numarasına sahip olması normaldir.

## DeleteBehavior.Restrict

Geçmiş finansal işlemde kullanılmış müşteri, hesap veya dövizin silinerek işlem geçmişinin bozulmasını engeller.

Cascade delete yerine restrict kullanılmasının nedeni finansal geçmişin referans bütünlüğünü korumaktır.

## Check constraint

Hesap hareketi türünün yalnızca şu değerlerden biri olması veritabanında da zorunlu tutulur:

```text
BORC
ALACAK
```

Uygulama yanlış değer gönderse bile veritabanı bu kaydı kabul etmez.

---

# Model dosyaları

## Models/Doviz.cs

`Dovizler` tablosunun C# karşılığıdır.

Alanları:

- `Id`: Veritabanı kimliği
- `Kod`: TRY, EUR, USD gibi para birimi kodu
- `Ad`: Para biriminin açıklaması
- `Birim`: TCMB kurunun kaç birim için verildiği
- `AktifMi`: Dövizin sistemde kullanılabilir olup olmadığı
- `OlusturmaTarihi`: Sisteme eklenme zamanı

Döviz kodunu her hesapta metin olarak tekrar etmek yerine hesaplarda `DovizId` tutulur. Böylece normalizasyon sağlanır ve aynı bilgi gereksiz yere tekrar edilmez.

## Models/Musteri.cs

`Musteriler` tablosunun C# karşılığıdır.

Müşterinin temel bilgilerini ve navigation property'lerini içerir.

`Hesaplar` collection'ı bir müşterinin birden fazla döviz hesabına sahip olabileceğini gösterir. Müşterinin şube bağlantısı da doğrudan bu modelde bulunur.

## Models/MusteriHesabi.cs

Müşteriye bağlı tek bir döviz hesabını temsil eder. Ayrı bir `Id` alanı yoktur; primary key `MusteriId + HesapEkNo` birleşimidir.

Örnek:

```text
Müşteri ID: 1
Hesap Ek No: 5002
Döviz: EUR
Bakiye: 100 EUR
```

Bakiyenin müşteri tablosunda tek alan olarak tutulmamasının nedeni her dövizin ayrı bakiyeye sahip olmasıdır.

Navigation property'ler üzerinden:

- Hesabın müşterisine ve müşterinin şubesine
- Hesabın dövizine
- Hesabın borçlu veya alacaklı olduğu işlemlere
- Hesabın hareketlerine

ulaşılabilir.

## Models/DovizIslemi.cs

Döviz dönüşümünün ana işlem kaydıdır.

Şu bilgileri saklar:

- Tekil referans numarası
- Borçlu hesap
- Alacaklı hesap
- Ödenen döviz ID'si
- Alınan döviz ID'si
- Ödenen ve alınan döviz tutarları
- Kullanılan alış ve satış kurları
- TL karşılığı
- İşlem tarihi

### ReferansNo neden Guid?

- İşlemi dış dünyada tekil tanımlamak
- Kullanıcıya takip numarası vermek
- İşlem hareketlerini ortak numara altında takip etmek
- Artan ve tahmin edilebilir veritabanı ID'sini dışarı açmamak

amacıyla kullanılır.

## Models/HesapHareketi.cs

Bir finansal işlemin tek bir hesaba etkisini temsil eder.

Bir döviz dönüşümünde normalde iki hareket oluşur:

```text
Ödenen döviz hesabı → BORC
Alınan döviz hesabı → ALACAK
```

Hesap ekstresi bu tablodan üretilebilir.

## Models/KurKaydi.cs

TCMB kurlarını tarihsel olarak saklamak amacıyla hazırlanmıştır.

Alanları:

- Döviz
- Kur tarihi
- Birim
- Alış kuru
- Satış kuru
- Oluşturulma tarihi

Mevcut uygulama TCMB kurunu anlık olarak alıyor fakat otomatik şekilde `KurKayitlari` tablosuna kaydetmiyor.

Sorulursa şu şekilde açıklanabilir:

> Kur geçmişini saklamak ve geçmiş tarihlere göre rapor üretmek için tabloyu modelledim; ancak TCMB kurlarını bu tabloya periyodik kaydedecek job mevcut aşamada henüz uygulanmadı.

---

# Database/005_FullDatabaseSetup.sql

Boş SQL Server veritabanında projenin bütün tablolarını ve sunum verilerini tek transaction içinde oluşturur. Hedef tablolardan biri zaten varsa mevcut verileri korumak için hiçbir değişiklik yapmadan hata verir.

Oluşturduğu temel bağlantılar:

```text
Subeler 1 ── N Musteriler
Musteriler 1 ── N MusteriHesaplari N ── 1 Dovizler
DovizIslemleri ── (MusteriId, Borclu/AlacakliHesapEkNo)
DovizIslemleri ── Odenen/Alinan Dovizler
```

Script ayrıca sekiz döviz, üç şube, dört müşteri, her müşteride 5001'den başlayan döviz hesapları, örnek kur kayıtları, dört referans numaralı döviz işlemi ve sekiz hesap hareketi ekler.

SQL uygulama tarafından otomatik çalıştırılmaz. Boş veritabanı seçildikten sonra kullanıcı tarafından MSSQL üzerinde bir defa elle çalıştırılır.

---

# Yapılandırma ve proje dosyaları

## appsettings.json

Genel loglama ve host ayarlarını içerir. Gerçek veritabanı şifresi burada tutulmaz.

## appsettings.Development.json

Development ortamına özel loglama ve uygulama ayarlarını içerir.

## appsettings.Local.json

Gerçek MSSQL connection string'i burada bulunur. Hassas bilgi içerdiği için Git'e gönderilmez.

## appsettings.Local.example.json

Projeyi alan başka bir geliştiriciye connection string'in hangi formatta olması gerektiğini gösterir. Gerçek kullanıcı adı veya şifre içermez.

## .gitignore

Git'e gönderilmemesi gereken dosyaları belirler:

- `bin`
- `obj`
- `.idea`
- `appsettings.Local.json`
- `.env`
- Geçici işletim sistemi dosyaları

Bir dosya `.gitignore` eklenmeden önce Git tarafından takip edilmeye başlandıysa yalnızca `.gitignore` eklemek takibi otomatik durdurmaz.

## DovizApi.csproj

Projenin teknik tanımını içerir:

- .NET 8 hedef framework'ü
- ASP.NET Core Web SDK
- Entity Framework Core SQL Server paketi
- Swagger/Swashbuckle paketi
- Nullable reference type ayarı
- Implicit using ayarı

`Nullable enable`, olası null hatalarını derleme aşamasında uyarı olarak göstermeye yardımcı olur.

`ImplicitUsings`, sık kullanılan namespace'lerin otomatik eklenmesini sağlar.

## DovizApi.sln

Rider ve Visual Studio'nun projeleri solution olarak yönetmesini sağlayan çözüm dosyasıdır.

## global.json

Projede kullanılacak .NET SDK sürüm ailesini belirler. Projenin farklı geliştirici bilgisayarlarında uyumlu SDK ile derlenmesine yardımcı olur.

## Properties/launchSettings.json

Uygulamanın local geliştirme ortamında hangi adreslerde çalışacağını belirler:

```text
http://localhost:5054
https://localhost:7117
```

Ayrıca:

- Development ortamını seçer.
- Swagger'ın başlangıçta açılmasını sağlar.
- HTTP ve HTTPS çalışma profillerini tanımlar.

## DovizApi.http

Rider veya Visual Studio içinden elle HTTP isteği göndermek için kullanılabilir. Mevcut dosyada eski `weatherforecast` şablon isteği bulunmaktadır ve güncel endpointlere göre düzenlenmemiştir.

## WeatherForecast.cs

ASP.NET Core proje şablonundan kalan örnek modeldir. Döviz projesinin mevcut işleyişinde kullanılmamaktadır ve daha sonra temizlenebilir.

---

# GET ve POST endpoint özeti

## GET endpointleri

GET istekleri veri okumak için kullanılır ve veritabanında değişiklik yapmamalıdır.

### GET /api/v1/kur-oku

TCMB'den anlık kur listesini getirir.

### GET /api/v1/dovizleri-getir

Veritabanında tanımlı aktif dövizleri getirir.

### GET /api/v1/doviz-islemleri-getir

Gerçekleşmiş bütün döviz işlemlerini getirir.

### GET /api/v1/musteriler

Bütün müşterileri getirir.

### GET /api/v1/musteriler/{musteriId}/hesaplar

Belirli müşterinin hesaplarını getirir.

### GET /api/v1/musteriler/{musteriId}/hesaplar/{hesapEkNo}/hareketler

Belirli müşterinin belirli ek numaralı hesabının hareketlerini getirir.

## POST endpointleri

POST istekleri yeni kayıt veya finansal işlem oluşturur.

### POST /api/v1/musteriler

Yeni müşteri ve otomatik Hesap Ek No 5001 TRY hesabı oluşturur.

Örnek body:

```json
{
  "ad": "Ahmet",
  "soyad": "Test",
  "subeKodu": "001",
  "baslangicTryBakiyesi": 10000
}
```

### POST /api/v1/musteriler/{musteriId}/hesaplar

Müşteriye yeni döviz hesabı açar.

Örnek body:

```json
{
  "dovizKodu": "EUR"
}
```

### POST /api/v1/doviz-cevir

İki döviz hesabı arasında dönüşüm yapar.

Örnek body:

```json
{
  "musteriId": 1,
  "borcluHesapEkNo": 5002,
  "alacakliHesapEkNo": 5001,
  "odenecekDovizMiktari": 1000
}
```

Bu örneğin anlamı:

```text
Hesap Ek No 5002 → Ödenecek döviz hesabı, BORC, bakiye azalır
Hesap Ek No 5001 → Alınacak döviz hesabı, ALACAK, bakiye artar
Ödenecek tutar → Hesap Ek No 5002 hesabının dövizinden 1.000 birim
```

---

# Projenin mevcut eksikleri ve geliştirme noktaları

Ana müşteri, hesap ve döviz dönüşümü senaryosu çalışmaktadır. Üretim seviyesi için kalan başlıca noktalar şunlardır:

- Gerçek authentication ve authorization sistemi yoktur.
- Para yatırma ve para çekme endpointleri yoktur.
- Başlangıç TRY bakiyesi ayrı bir hesap hareketi olarak kaydedilmemektedir.
- Otomatik unit ve integration test projesi bulunmamaktadır.
- Eş zamanlı bakiye güncellemeleri için ileride RowVersion gibi ek optimistic concurrency kontrolü düşünülebilir.
- TCMB XML tarih alanında null güvenliği uyarısı bulunmaktadır.
- `KurKayitlari` tablosuna otomatik kur kaydeden job henüz yoktur.
- `WeatherForecast.cs` kullanılmamaktadır.
- `DovizApi.http` eski örnek endpointi içermektedir.
- `DovizController` sınıfının bulunduğu dosyanın adı standartla uyumlu değildir.

---

# Projeyi sözlü olarak özetleme

Projeyi anlatman istendiğinde şu açıklama kullanılabilir:

> Projeyi ASP.NET Core Web API ve Entity Framework Core kullanarak katmanlı şekilde geliştirdim. Controller'ları yalnızca HTTP istek ve cevaplarından sorumlu tuttum, finansal iş kurallarını service katmanına taşıdım. Controller ile servis arasındaki bağımlılığı azaltmak ve testlerde mock kullanabilmek için interface kullandım. TCMB bağlantısını IHttpClientFactory üzerinden yönetip XML kurlarını invariant culture ile parse ettim. Müşterilerin her dövizini ek numaralı ayrı hesap olarak modelledim. Döviz dönüşümünde borçlu hesabı ödeyen kaynak hesap olarak kullanıp bakiyesini azaltıyor, alacaklı hesabı alınan dövizin hedef hesabı olarak kullanıp bakiyesini artırıyorum. Bakiye güncellemesi, ana işlem kaydı ve iki hesap hareketini tek SQL transaction'ında kaydederek veri bütünlüğünü koruyorum. Request ve response DTO'larıyla veritabanı entity'lerini doğrudan dışarı açmıyorum.

## IDovizIslemService sorusuna kısa cevap

> Controller'ın gerçek servis sınıfına sıkı bağlanmaması için interface kullandım. Böylece Dependency Injection ile implementasyonu değiştirebilirim, unit testte mock servis verebilirim ve controller işin nasıl yapıldığını bilmeden yalnızca servis sözleşmesini kullanır. Tek implementasyonlu küçük bir projede zorunlu değildir fakat test edilebilirlik ve bağımlılıkların ayrılması için tercih ettim.

## Transaction sorusuna kısa cevap

> Bakiye değişiklikleri, ana döviz işlemi ve iki hesap hareketi birbirinden ayrılmaması gereken tek bir iş birimidir. Herhangi biri başarısız olduğunda diğerlerinin de geri alınması için hepsini aynı transaction içinde kaydettim.

## Request ve Response DTO sorusuna kısa cevap

> Veritabanı entity'lerini doğrudan dışarı açmamak, kullanıcının sistem alanlarını değiştirmesini engellemek ve API sözleşmesini veritabanı şemasından ayırmak için request ve response DTO'ları kullandım.

## AsNoTracking sorusuna kısa cevap

> Yalnızca okuma yaptığım sorgularda Entity Framework'ün nesneleri takip etmesine ihtiyaç olmadığı için performans ve bellek kullanımı açısından AsNoTracking kullandım.

## Decimal sorusuna kısa cevap

> Float ve double finansal hesaplarda binary hassasiyet hataları oluşturabildiği için para ve kur alanlarında decimal kullandım.

## IHttpClientFactory sorusuna kısa cevap

> HttpClient bağlantı yaşam döngüsünü doğru yönetmek, socket tüketimi sorunlarını önlemek ve TCMB adresi ile timeout ayarlarını merkezi tutmak için IHttpClientFactory kullandım.
