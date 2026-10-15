using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using VehicleTracking.Api.Health;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Api.Workers;
using VehicleTracking.Application;
using VehicleTracking.Application.Live;
using VehicleTracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IngestionHealthCheck>();
builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks()
    .AddCheck<IngestionHealthCheck>("ingestion", tags: ["ready"]);

// Kayıt sırası önemli: hosted service'ler bu sırayla başlar, TERS sırayla durur.
// 1) DatabaseInitializer (AddInfrastructure içinde): DB hazır olmadan diğerleri başlamaz.
// 2) Yazıcı, 3) Çekici. Kapanışta önce çekici durur (tampona yeni parti gelmez), sonra yazıcı tamponu boşaltır.
builder.Services.AddHostedService<PositionWriterWorker>();
builder.Services.AddHostedService<FeedPollingWorker>();

// Canlı yayın. Enum'lar sayı yerine metin ("StoppedAt"): frontend'de okunur, enum sırası değişse de bozulmaz.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<LiveUpdateDispatcher>();
builder.Services.AddSingleton<ILiveUpdatePublisher>(sp => sp.GetRequiredService<LiveUpdateDispatcher>());

// REST'te de enum'lar metin olarak (SignalR ile tutarlı).
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// Frontend başka bir adresten (geliştirmede localhost:3000) bağlanıyor; tarayıcı buna ancak CORS izniyle izin verir.
// AllowCredentials: SignalR bağlantısı için gerekli; bu yüzden "her adrese izin" (*) yerine adresler config'ten tek tek verilir.
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// Başlangıç toleransı ilk health isteğinden değil, uygulama açılışından itibaren ölçülsün.
_ = app.Services.GetRequiredService<IngestionHealthCheck>();

// Swagger her ortamda açık: değerlendirici docker compose ile (Production) çalıştırıp Swagger'dan test edecek.
// .NET'in kendi ürettiği OpenAPI belgesi (/openapi/v1.json) + Swagger arayüzü (/swagger).
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Vehicle Tracking API v1"));

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHub<VehiclesHub>("/hubs/vehicles");

app.Run();
