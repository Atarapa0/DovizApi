using System.Text.Json;
using DovizApi.Data;
using DovizApi.Exceptions;
using DovizApi.Infrastructure;
using DovizApi.Options;
using DovizApi.Responses;
using DovizApi.Services;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    // Global handler exception'ı bir kez structured olarak loglar.
    .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "DovizApi")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console();

ElasticSinkEkle(loggerConfiguration, builder.Configuration, builder.Environment);
builder.Host.UseSerilog(loggerConfiguration.CreateLogger(), dispose: true);

builder.Services.Configure<HataLoglamaOptions>(
    builder.Configuration.GetSection(HataLoglamaOptions.SectionName));
builder.Services.AddSingleton<RequestVerisiTemizleyici>();
builder.Services.AddSingleton<HataLogService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var mesaj = context.ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? "Gönderilen bilgiler geçersiz.";

            return ApiHataSonucu.Olustur(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "GECERSIZ_ISTEK",
                mesaj,
                new GecersizIstekException("GECERSIZ_ISTEK", mesaj));
        };
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        builder.Environment.IsDevelopment()
            ? "DefaultConnection bulunamadı. appsettings.Local.json dosyasını oluşturmalısın."
            : "DefaultConnection bulunamadı. Production ortamında " +
              "ConnectionStrings__DefaultConnection environment variable değerini tanımlamalısın.");
}

// Factory hem request scope için DovizDbContext'i hem de bağımsız log DbContext'lerini sağlar.
builder.Services.AddDbContextFactory<DovizDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHttpClient("Tcmb", client =>
{
    client.BaseAddress = new Uri("https://www.tcmb.gov.tr/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ITcmbKurService, TcmbKurService>();
builder.Services.AddScoped<IDovizIslemService, DovizIslemService>();
builder.Services.AddScoped<IArbitrajService, ArbitrajService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SupportNonNullableReferenceTypes());

var app = builder.Build();
var swaggerEnabled = app.Environment.IsDevelopment() ||
                     builder.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<RequestLogHazirlamaMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    if (context.Response.HasStarted || context.Response.ContentLength is > 0)
    {
        return;
    }

    var status = context.Response.StatusCode;
    var (kod, mesaj) = status switch
    {
        StatusCodes.Status404NotFound => ("KAYNAK_BULUNAMADI", "İstenen kaynak bulunamadı."),
        StatusCodes.Status405MethodNotAllowed => ("METOT_DESTEKLENMIYOR", "Bu HTTP metodu desteklenmiyor."),
        _ => ("ISTEK_BASARISIZ", "İstek tamamlanamadı.")
    };
    var service = context.RequestServices.GetRequiredService<HataLogService>();
    var kayit = service.KayitOlustur(
        context,
        new InvalidOperationException(mesaj),
        status,
        kod,
        mesaj,
        kritik: false);
    await service.KaydetVeLoglaAsync(kayit);
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(
        ApiHataSonucu.ResponseOlustur(kayit),
        options: null,
        contentType: "application/problem+json");
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
})).ExcludeFromDescription();

if (swaggerEnabled)
{
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.Run();

static void ElasticSinkEkle(
    LoggerConfiguration loggerConfiguration,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    if (!bool.TryParse(configuration["ELASTICSEARCH_ENABLED"], out var etkin) || !etkin)
    {
        return;
    }

    if (!Uri.TryCreate(configuration["ELASTICSEARCH_URL"], UriKind.Absolute, out var uri))
    {
        Console.Error.WriteLine("Elastic logging etkin ancak ELASTICSEARCH_URL geçerli değil; console logging ile devam ediliyor.");
        return;
    }

    var indexPrefix = NormalizeDataStreamPart(
        configuration["ELASTICSEARCH_INDEX_PREFIX"] ?? "doviz-api");
    var environmentName = NormalizeDataStreamPart(environment.EnvironmentName);
    var username = configuration["ELASTICSEARCH_USERNAME"];
    var password = configuration["ELASTICSEARCH_PASSWORD"];

    // Elastic'e yalnızca merkezi hata olaylarını gönder; EF SQL komutları ve normal trafik taşınmaz.
    loggerConfiguration.WriteTo.Logger(elasticLogger =>
        elasticLogger
            .Filter.ByIncludingOnly(Matching.FromSource<HataLogService>())
            .WriteTo.Elasticsearch(
                [uri],
                options =>
                {
                    options.DataStream = new DataStreamName("logs", indexPrefix, environmentName);
                    options.BootstrapMethod = BootstrapMethod.Silent;
                },
                transport =>
                {
                    if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        transport.Authentication(new BasicAuthentication(username, password));
                    }
                }));
}

static string NormalizeDataStreamPart(string value)
{
    var normalized = new string(value
        .Trim()
        .ToLowerInvariant()
        .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
            ? character
            : '-')
        .ToArray());
    return string.IsNullOrWhiteSpace(normalized) ? "default" : normalized;
}
