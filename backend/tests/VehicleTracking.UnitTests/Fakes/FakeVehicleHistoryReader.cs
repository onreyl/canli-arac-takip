using VehicleTracking.Application.History;

namespace VehicleTracking.UnitTests.Fakes;

internal sealed class FakeVehicleHistoryReader : IVehicleHistoryReader
{
    public HistoryPage Page { get; init; } = new([], null);
    public int CallCount { get; private set; }
    public string? VehicleId { get; private set; }
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset To { get; private set; }
    public int PageSize { get; private set; }
    public HistoryCursor? After { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    public Task<HistoryPage> GetPageAsync(string vehicleId, DateTimeOffset from, DateTimeOffset to,
        int pageSize, HistoryCursor? after, CancellationToken cancellationToken)
    {
        CallCount++;
        (VehicleId, From, To, PageSize, After, CancellationToken) = (vehicleId, from, to, pageSize, after, cancellationToken);
        return Task.FromResult(Page);
    }
}
