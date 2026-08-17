# DovizApi Dokploy Kurulumu

Bu kurulum API ve SQL Server'ı aynı Dokploy Compose projesinde çalıştırır. SQL Server internete açılmaz; yalnızca API container'ı üzerinden erişilir. Kalıcı veriler `sqlserver_data` adlı Docker volume'unda tutulur.

## 1. Sunucu gereksinimi

- Linux `amd64` sunucu kullan.
- SQL Server nedeniyle en az 4 GB RAM önerilir.
- Sunucunun 80 ve 443 portları internete açık olmalıdır.
- Dokploy sunucuda kurulu ve alan adının DNS yönetimine erişimin olmalıdır.

> Compose dosyasında SQL Server Developer sürümü kullanılıyor. Bu yapı staj, geliştirme ve gösterim ortamı içindir. Ticari canlı kullanımda lisanslı SQL Server sürümüne veya yönetilen bir SQL hizmetine geçilmelidir.

## 2. Kodu GitHub'a gönder

Dokploy'un görebilmesi için bu değişiklikleri commit edip `master` dalına push et:

```bash
git add .dockerignore .env.example Dockerfile docker-compose.dokploy.yml \
  Database/Dockerfile Database/docker-entrypoint.sh \
  Database/005_FullDatabaseSetup.sql Database/006_AddDovizIslemiTersKayit.sql \
  Database/007_AddHataLoglari.sql \
  DovizApi/Program.cs DovizApi/appsettings.json DOKPLOY_KURULUMU.md
git commit -m "chore: Dokploy dağıtım yapılandırmasını ekle"
git push origin master
```

`.env` dosyasını veya gerçek parolaları commit etme.

## 3. Dokploy Compose projesini oluştur

1. Dokploy'da bir proje oluştur.
2. Proje içinde **Compose** türünde bir servis ekle.
3. Kaynak olarak GitHub reposunu seç: `Atarapa0/DovizApi`.
4. Branch değerini `master` yap.
5. Compose yolu olarak `./docker-compose.dokploy.yml` gir.
6. Dokploy'un **Environment** bölümüne aşağıdaki değerleri ekle:

```dotenv
MSSQL_SA_PASSWORD=BURAYA_EN_AZ_16_KARAKTER_GUCLU_PAROLA
ELASTICSEARCH_ENABLED=false
ELASTICSEARCH_URL=
ELASTICSEARCH_USERNAME=
ELASTICSEARCH_PASSWORD=
ELASTICSEARCH_INDEX_PREFIX=doviz-api
```

`MSSQL_SA_PASSWORD` büyük harf, küçük harf, rakam ve özel karakter içermelidir. Gerçek değeri yalnızca Dokploy ortam değişkenlerinde sakla.

7. **Deploy** düğmesine bas. İlk çalıştırmada SQL başlangıç scriptleri `dovizDb` veritabanını, tabloları ve başlangıç dövizlerini oluşturur.

## 4. Alan adını bağla

DNS sağlayıcında şu kaydı oluştur:

| Tür | Ad | Değer | TTL |
|---|---|---|---|
| A | `staj-api` | Dokploy sunucusunun public IPv4 adresi | Auto |

Dokploy Compose servisinin **Domains** bölümünde yeni domain ekle:

- Host: `staj-api.furkanerdogan.com`
- Service: `api`
- Path: `/`
- Internal path: `/`
- Container port: `8080`
- HTTPS: Açık
- Certificate: Let's Encrypt

Domain değişikliğinden sonra Compose servisini yeniden deploy et.

## 5. Yayın kontrolü

Tarayıcıdan veya terminalden kontrol et:

```bash
curl -i https://staj-api.furkanerdogan.com/health
curl -i https://staj-api.furkanerdogan.com/api/v1/dovizleri-getir
```

Swagger adresi:

```text
https://staj-api.furkanerdogan.com/swagger
```

Beklenen sağlık cevabı:

```json
{
  "status": "healthy",
  "timestamp": "2026-08-17T14:14:54.6160376Z"
}
```

## 6. Frontend bağlantısı

Next.js projesinde backend adresini kullanan environment variable'ı şu adrese ayarla:

```dotenv
BACKEND_API_URL=https://staj-api.furkanerdogan.com
```

Frontend farklı bir değişken adı kullanıyorsa aynı URL'yi o değişkene ver.

## 7. Güvenlik ve bakım

- SQL Server'ın 1433 portu public değildir; bu bilinçli bir güvenlik ayarıdır.
- `sqlserver_data` volume'u için Dokploy üzerinden düzenli yedek oluştur.
- Elasticsearch zorunlu değildir; kapalıyken SQL ve console loglama devam eder.
- Bu API'de henüz kimlik doğrulama/yetkilendirme yok. İnternete açık demo ortamını Cloudflare Access, IP kısıtı veya Dokploy middleware'i ile koru. Gerçek kullanımdan önce JWT/API key gibi uygulama seviyesinde yetkilendirme ekle.
- Sunumdan sonra Swagger'ı kapatmak için Compose dosyasında `Swagger__Enabled` değerini `false` yapıp yeniden deploy edebilirsin.
- Backend tamamen kapalıysa backend'in SQL hata logu oluşturamayacağı unutulmamalıdır; bu durumda proxy/Traefik erişim logları incelenir.

## 8. Sorun giderme

- API başlamıyorsa önce `sqlserver` servisinin health durumunu ve logunu kontrol et.
- `Login failed for user 'sa'` görülürse Dokploy'daki `MSSQL_SA_PASSWORD` değerini kontrol et.
- Alan adı açılmıyorsa A kaydının doğru sunucu IP'sine çözüldüğünü doğrula.
- HTTPS sertifikası oluşmuyorsa 80/443 portlarını, DNS çözümünü ve Dokploy Traefik loglarını kontrol et.
- Parola daha sonra değiştirilirse mevcut SQL volume'undaki `sa` parolası otomatik değişmez. Parola değişikliği SQL içinde ayrıca uygulanmalıdır.
