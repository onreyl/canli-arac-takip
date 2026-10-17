using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.UnitTests.Application;

// Eski feed ve ölçümler elenir; aynı aracın farklı seferleri ayrı varlık olarak korunur.
// Gerçek feed'in yarısından fazlası eski ölçümdür, feed zamanı turların yaklaşık %15'inde geri gider.
public sealed class FeedIngestorTests
{
    [Fact]
    public void Process_FirstFeed_ReturnsOnlyPositionsWithinRetention()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        var recent = TestData.Position("recent", "vehicle-1", TestData.Now.AddMinutes(-1));
        var boundary = TestData.Position("boundary", "vehicle-2", TestData.Now.AddHours(-3));
        var expired = TestData.Position("expired", "vehicle-3", TestData.Now.AddHours(-3).AddSeconds(-1));
        var feed = new FeedSnapshot(TestData.Now, [recent, boundary, expired], 0);

        var result = ingestor.Process(feed, TestData.Now);

        Assert.False(result.IsStale);
        Assert.Equal([recent, boundary], result.NewPositions);
    }

    [Fact]
    public void Process_SameGeneratedAt_ReturnsStale()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        var position = TestData.Position("entity", "vehicle", TestData.Now);
        var feed = new FeedSnapshot(TestData.Now, [position], 0);
        ingestor.Process(feed, TestData.Now);

        var result = ingestor.Process(feed, TestData.Now);

        Assert.True(result.IsStale);
        Assert.Empty(result.NewPositions);
    }

    [Fact]
    public void Process_OlderGeneratedAt_ReturnsStale()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        ingestor.Process(new FeedSnapshot(TestData.Now, [], 0), TestData.Now);
        var position = TestData.Position("entity", "vehicle", TestData.Now.AddSeconds(-1));
        var feed = new FeedSnapshot(TestData.Now.AddSeconds(-1), [position], 0);

        var result = ingestor.Process(feed, TestData.Now);

        Assert.True(result.IsStale);
        Assert.Empty(result.NewPositions);
    }

    [Fact]
    public void Process_RepeatedEntityAndMeasurement_ReturnsNoPositions()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        var position = TestData.Position("entity", "vehicle", TestData.Now.AddMinutes(-1));
        ingestor.Process(new FeedSnapshot(TestData.Now.AddSeconds(-3), [position], 0), TestData.Now);

        var result = ingestor.Process(new FeedSnapshot(TestData.Now, [position], 0), TestData.Now);

        Assert.False(result.IsStale);
        Assert.Empty(result.NewPositions);
    }

    [Fact]
    public void Process_SameEntityWithNewerMeasurement_ReturnsNewPosition()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        var older = TestData.Position("entity", "vehicle", TestData.Now.AddMinutes(-1));
        var newer = TestData.Position("entity", "vehicle", TestData.Now);
        ingestor.Process(new FeedSnapshot(TestData.Now.AddSeconds(-3), [older], 0), TestData.Now);

        var result = ingestor.Process(new FeedSnapshot(TestData.Now, [newer], 0), TestData.Now);

        Assert.False(result.IsStale);
        Assert.Equal(newer, Assert.Single(result.NewPositions));
    }

    [Fact]
    public void Process_SameVehicleWithDifferentEntities_ReturnsBothPositions()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        var firstTrip = TestData.Position("entity-1", "vehicle", TestData.Now);
        var secondTrip = TestData.Position("entity-2", "vehicle", TestData.Now);

        var result = ingestor.Process(new FeedSnapshot(TestData.Now, [firstTrip, secondTrip], 0), TestData.Now);

        Assert.False(result.IsStale);
        Assert.Equal([firstTrip, secondTrip], result.NewPositions);
    }

    [Fact]
    public void Process_NullGeneratedAt_ProcessesFeed()
    {
        var ingestor = new FeedIngestor(TestData.StorageOptions);
        ingestor.Process(new FeedSnapshot(TestData.Now, [], 0), TestData.Now);
        var position = TestData.Position("entity", "vehicle", TestData.Now);

        var result = ingestor.Process(new FeedSnapshot(null, [position], 0), TestData.Now);

        Assert.False(result.IsStale);
        Assert.Equal(position, Assert.Single(result.NewPositions));
    }
}
