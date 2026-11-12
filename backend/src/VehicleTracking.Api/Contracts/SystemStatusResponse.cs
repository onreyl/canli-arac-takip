namespace VehicleTracking.Api.Contracts;

/// <param name="LastSuccessfulFetchAt">Kaynaktan son başarılı okuma zamanı.</param>
/// <param name="LastFetchDurationMs">Son okumanın süresi (çekme + çözme).</param>
/// <param name="WrittenLastHour">Son 1 saatte DB'ye yazılan (yeni) kayıt sayısı.</param>
/// <param name="FailedFetchCount">Başarısız okuma denemesi (kaynak erişilemez / bozuk yanıt).</param>
/// <param name="FailedWriteCount">Başarısız yazma denemesi (DB erişilemez); her tekrar ayrı sayılır.</param>
/// <param name="DroppedBatchCount">Kaybolan parti (tampon taştı ya da kalıcı hata). Sıfır olmalı.</param>
/// <param name="ConnectedClients">Bağlı WebSocket (SignalR) istemcisi.</param>
/// <param name="PendingBatches">Tamponda DB'ye yazılmayı bekleyen parti.</param>
/// <param name="LiveVehicles">Canlı durumdaki araç sayısı.</param>
/// <param name="LastWriteDurationMs">Son başarılı DB yazmasının süresi (milisaniye); henüz yazma yoksa null.</param>
/// <param name="MaxWriteDurationLastHourMs">Son 1 saatteki en uzun başarılı DB yazmasının süresi (milisaniye); pencerede yazma yoksa null.</param>
public sealed record SystemStatusResponse(
    DateTimeOffset? LastSuccessfulFetchAt,
    double? LastFetchDurationMs,
    long WrittenLastHour,
    long FailedFetchCount,
    long FailedWriteCount,
    long DroppedBatchCount,
    int ConnectedClients,
    int PendingBatches,
    int LiveVehicles,
    double? LastWriteDurationMs,
    double? MaxWriteDurationLastHourMs);
