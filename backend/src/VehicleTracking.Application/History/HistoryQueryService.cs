using FluentValidation;
using Microsoft.Extensions.Options;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.Application.History;

/// <summary>Doğrulanmış geçmiş sorgusunu kalıcı depoya iletir.</summary>
public sealed class HistoryQueryService(
    IValidator<ResolvedHistoryQuery> validator,
    IVehicleHistoryReader reader,
    TimeProvider timeProvider,
    IOptions<StorageOptions> storageOptions,
    IOptions<HistoryOptions> historyOptions) : IHistoryQueryService
{
    /// <summary>Geçmiş sayfasını veya doğrulama hatalarını döndürür.</summary>
    public async Task<HistoryQueryResult> QueryAsync(HistoryQuery query, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var resolved = ResolvedHistoryQuery.Resolve(query, now, storageOptions.Value.Retention, historyOptions.Value);
        var validation = await validator.ValidateAsync(resolved, cancellationToken);
        if (!validation.IsValid)
            return HistoryQueryResult.Invalid(validation.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));

        // Pencere başından eskisini istese bile (tolerans içinde) pencereye kırpıyoruz:
        // chunk bütün olarak silindiği için DB'de saklama süresinden biraz eski satır kalabilir, kullanıcı görmemeli.
        DateTimeOffset start = resolved.Start < resolved.WindowStart ? resolved.WindowStart : resolved.Start;

        HistoryPage page = await reader.GetPageAsync(resolved.VehicleId, start, resolved.End, resolved.PageSize, resolved.After, cancellationToken);
        return HistoryQueryResult.Success(new HistoryPageResponse(
            page.Items.Select(HistoryPointDto.From).ToList(),
            page.Next is null ? null : HistoryCursorCodec.Encode(page.Next)));
    }
}
