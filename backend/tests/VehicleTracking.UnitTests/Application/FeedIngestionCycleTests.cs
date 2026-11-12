using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Application.Live;
using VehicleTracking.UnitTests.Fakes;

namespace VehicleTracking.UnitTests.Application;

public sealed class FeedIngestionCycleTests
{
    [Fact]
    public async Task RunOnceAsync_FeedUnavailable_RecordsFailureWithoutPublishingOrEnqueuing()
    {
        List<string> calls = [];
        var source = new FakeFeedSource
        {
            Failure = new FeedUnavailableException("Kaynak kullanılamıyor", new TimeoutException())
        };
        var buffer = new FakeFeedBuffer(calls);
        var publisher = new FakeLiveUpdatePublisher(calls);
        var clock = new FakeTimeProvider(TestData.Now);
        var metrics = new IngestionMetrics(clock);
        var cycle = CreateCycle(source, buffer, publisher, metrics, clock);
        long failuresBefore = metrics.GetSnapshot().FailedFetchCount;

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Equal(failuresBefore + 1, metrics.GetSnapshot().FailedFetchCount);
        Assert.Null(metrics.GetSnapshot().LastSuccessfulFetchAt);
        Assert.Empty(buffer.Batches);
        Assert.Empty(publisher.Updates);
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunOnceAsync_NewPosition_PublishesBeforeEnqueuingEvenIfPublisherFails(bool publisherFails)
    {
        List<string> calls = [];
        var position = TestData.Position("entity", "vehicle", TestData.Now);
        var source = new FakeFeedSource { Snapshot = new FeedSnapshot(TestData.Now, [position]) };
        var buffer = new FakeFeedBuffer(calls);
        var publisher = new FakeLiveUpdatePublisher(calls)
        {
            Failure = publisherFails ? new InvalidOperationException("Yayın başarısız") : null
        };
        var clock = new FakeTimeProvider(TestData.Now);
        var metrics = new IngestionMetrics(clock);
        var cycle = CreateCycle(source, buffer, publisher, metrics, clock);

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Equal(["publish", "enqueue"], calls);
        Assert.Equal(position, Assert.Single(Assert.Single(publisher.Updates).Upserts));
        Assert.Equal(position, Assert.Single(Assert.Single(buffer.Batches).Positions));
        Assert.Equal(TestData.Now, metrics.GetSnapshot().LastSuccessfulFetchAt);
        Assert.NotNull(metrics.GetSnapshot().LastFetchDuration);
        Assert.Equal(0, metrics.GetSnapshot().FailedFetchCount);
    }

    [Fact]
    public async Task RunOnceAsync_SameGeneratedAt_DoesNotEnqueueOrPublishAgain()
    {
        List<string> calls = [];
        var position = TestData.Position("entity-1", "vehicle-1", TestData.Now);
        var source = new FakeFeedSource { Snapshot = new FeedSnapshot(TestData.Now, [position]) };
        var buffer = new FakeFeedBuffer(calls);
        var publisher = new FakeLiveUpdatePublisher(calls);
        var clock = new FakeTimeProvider(TestData.Now);
        var metrics = new IngestionMetrics(clock);
        var cycle = CreateCycle(source, buffer, publisher, metrics, clock);
        await cycle.RunOnceAsync(CancellationToken.None);
        source.Snapshot = new FeedSnapshot(TestData.Now,
            [TestData.Position("entity-2", "vehicle-2", TestData.Now)]);

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Equal(position, Assert.Single(Assert.Single(buffer.Batches).Positions));
        Assert.Single(publisher.Updates);
        Assert.Equal(["publish", "enqueue"], calls);
    }

    private static FeedIngestionCycle CreateCycle(
        IFeedSource source,
        IFeedBuffer buffer,
        ILiveUpdatePublisher publisher,
        IIngestionMetrics metrics,
        TimeProvider clock) =>
        new(source, new FeedIngestor(TestData.StorageOptions), buffer,
            new LiveVehicleState(TestData.StorageOptions), publisher, metrics, clock,
            NullLogger<FeedIngestionCycle>.Instance);
}
