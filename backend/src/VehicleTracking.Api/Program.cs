using VehicleTracking.Api.Workers;
using VehicleTracking.Application;
using VehicleTracking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// Kayıt sırası önemli: hosted service'ler bu sırayla başlar, TERS sırayla durur.
// 1) DatabaseInitializer (AddInfrastructure içinde): DB hazır olmadan diğerleri başlamaz.
// 2) Yazıcı, 3) Çekici. Kapanışta önce çekici durur (tampona yeni parti gelmez), sonra yazıcı tamponu boşaltır.
builder.Services.AddHostedService<PositionWriterWorker>();
builder.Services.AddHostedService<FeedPollingWorker>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
