using DovizApi.Data;
using DovizApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Local.json",
        optional: true,
        reloadOnChange: true);
}

// Add services to the container.

builder.Services.AddControllers();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection bulunamadı. appsettings.Local.json dosyasını oluşturmalısın.");

builder.Services.AddDbContext<DovizDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHttpClient("Tcmb", client =>
{
    client.BaseAddress = new Uri("https://www.tcmb.gov.tr/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<ITcmbKurService, TcmbKurService>();
builder.Services.AddScoped<IDovizIslemService, DovizIslemService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
