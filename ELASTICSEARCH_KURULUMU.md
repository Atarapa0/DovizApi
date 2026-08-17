# Elasticsearch / Kibana hata logu kurulumu

Uygulama Elastic olmadan çalışır. Elastic loglamayı açmak için aşağıdaki environment
variable'ları deployment ortamında tanımlayın; parola veya kullanıcı adını repoya yazmayın.

```text
ELASTICSEARCH_ENABLED=true
ELASTICSEARCH_URL=https://elastic-sunucu:9200
ELASTICSEARCH_USERNAME=<kullanici>
ELASTICSEARCH_PASSWORD=<parola>
ELASTICSEARCH_INDEX_PREFIX=doviz-api
```

Resmî `Elastic.Serilog.Sinks` ECS data stream yaklaşımı kullanılır. Varsayılan data stream:

```text
logs-doviz-api-development
logs-doviz-api-production
```

Kibana Discover'da `logs-doviz-api-*` data view oluşturun. Örnek KQL sorguları:

```text
labels.HataId : "ERR-5AA358934DF0"
labels.CorrelationId : "test-correlation-400"
labels.HttpStatus >= 500
labels.HataKodu : "BAKIYE_YETERSIZ"
```

ECS formatter özel structured alanları `labels.*` altında indeksler; `@timestamp`,
`message`, `log.level` ve hata bilgileri standart ECS alanlarında tutulur.

SQL tablosunu oluşturmak için `Database/007_AddHataLoglari.sql` scriptini hedef
veritabanında bir kez çalıştırın. Script tekrar çalıştırılabilir ve mevcut tabloları değiştirmez.

Varsayılan SQL log politikası 500 ve 503 kayıtlarıdır. Kritik 409 kodlarını
`HataLoglama:SqlKritik409HataKodlari` listesine ekleyebilirsiniz. Tüm beklenen
400/404/409 hatalarını SQL'e de yazmak için `SqlBeklenenHatalariKaydet=true` yapın.
