using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.UnitTests.Fakes;

internal sealed class FakeFeedSource : IFeedSource
{
    public FeedSnapshot Snapshot { get; set; } = new(null, []);
    public Exception? Failure { get; init; }

    public Task<FeedSnapshot> FetchAsync(CancellationToken cancellationToken) =>
        Failure is { } failure ? Task.FromException<FeedSnapshot>(failure) : Task.FromResult(Snapshot);
}
