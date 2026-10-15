using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Application.Ingestion;

namespace VehicleTracking.UnitTests.Application;

public sealed class IngestionMetricsTests
{
    [Fact]
    public void GetSnapshot_NoWrites_ReturnsNullDurations()
    {
        var metrics = new IngestionMetrics(new FakeTimeProvider(TestData.Now));

        var snapshot = metrics.GetSnapshot();

        Assert.Null(snapshot.LastWriteDuration);
        Assert.Null(snapshot.MaxWriteDurationLastHour);
        Assert.Equal(0, snapshot.WrittenLastHour);
    }

    [Fact]
    public void RecordWritten_MultipleWrites_TracksLastAndMaximumDuration()
    {
        var metrics = new IngestionMetrics(new FakeTimeProvider(TestData.Now));
        metrics.RecordWritten(10, TimeSpan.FromMilliseconds(100));
        metrics.RecordWritten(20, TimeSpan.FromMilliseconds(300));
        metrics.RecordWritten(30, TimeSpan.FromMilliseconds(200));
        metrics.RecordWriteFailure();

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(TimeSpan.FromMilliseconds(200), snapshot.LastWriteDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(300), snapshot.MaxWriteDurationLastHour);
        Assert.Equal(60, snapshot.WrittenLastHour);
        Assert.Equal(1, snapshot.FailedWriteCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSnapshot_WriteOlderThanOneHour_ExcludesCountAndDuration(bool recordNewWrite)
    {
        var clock = new FakeTimeProvider(TestData.Now);
        var metrics = new IngestionMetrics(clock);
        metrics.RecordWritten(10, TimeSpan.FromMilliseconds(300));
        clock.Advance(TimeSpan.FromMinutes(30));
        metrics.RecordWritten(20, TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromMinutes(30));
        var atBoundary = metrics.GetSnapshot();
        Assert.Equal(30, atBoundary.WrittenLastHour);
        Assert.Equal(TimeSpan.FromMilliseconds(300), atBoundary.MaxWriteDurationLastHour);
        clock.Advance(TimeSpan.FromTicks(1));

        if (recordNewWrite)
        {
            metrics.RecordWritten(5, TimeSpan.FromMilliseconds(50));
        }
        var snapshot = metrics.GetSnapshot();

        Assert.Equal(recordNewWrite ? 25 : 20, snapshot.WrittenLastHour);
        Assert.Equal(TimeSpan.FromMilliseconds(100), snapshot.MaxWriteDurationLastHour);
        Assert.Equal(TimeSpan.FromMilliseconds(recordNewWrite ? 50 : 100), snapshot.LastWriteDuration);
    }

    [Fact]
    public void GetSnapshot_AllWritesExpired_PreservesLastDurationAndClearsWindow()
    {
        var clock = new FakeTimeProvider(TestData.Now);
        var metrics = new IngestionMetrics(clock);
        metrics.RecordWritten(10, TimeSpan.FromMilliseconds(100));
        clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromTicks(1));

        var snapshot = metrics.GetSnapshot();

        Assert.Equal(0, snapshot.WrittenLastHour);
        Assert.Null(snapshot.MaxWriteDurationLastHour);
        Assert.Equal(TimeSpan.FromMilliseconds(100), snapshot.LastWriteDuration);
    }
}
