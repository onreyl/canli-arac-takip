using System.Text.Json.Serialization;
using VehicleTracking.Api.Realtime;
using VehicleTracking.Api.Workers;
using VehicleTracking.Application;
using VehicleTracking.Application.Live;
using VehicleTracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

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

var app = builder.Build();

// Swagger her ortamda açık: değerlendirici docker compose ile (Production) çalıştırıp Swagger'dan test edecek.
// .NET'in kendi ürettiği OpenAPI belgesi (/openapi/v1.json) + Swagger arayüzü (/swagger).
app.MapOpenApi();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Vehicle Tracking API v1"));

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<VehiclesHub>("/hubs/vehicles");

app.Run();
