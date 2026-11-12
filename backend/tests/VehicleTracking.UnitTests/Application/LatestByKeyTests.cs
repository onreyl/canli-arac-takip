using VehicleTracking.Application.Common;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.UnitTests.Application;

public sealed class LatestByKeyTests
{
    [Fact]
    public void TryUpdate_NewKey_AddsValue()
    {
        var latest = new LatestByKey<DateTimeOffset>(timestamp => timestamp);

        bool updated = latest.TryUpdate("entity", TestData.Now);

        Assert.True(updated);
        Assert.Equal(1, latest.Count);
        Assert.Equal(TestData.Now, Assert.Single(latest.Values));
    }

    [Fact]
    public void TryUpdate_NewerTimestamp_ReplacesValue()
    {
        var latest = new LatestByKey<VehiclePosition>(position => position.MeasuredAt);
        var older = TestData.Position("older", "vehicle", TestData.Now.AddTicks(-1));
        var newer = TestData.Position("newer", "vehicle", TestData.Now);
        latest.TryUpdate("vehicle", older);

        bool updated = latest.TryUpdate("vehicle", newer);

        Assert.True(updated);
        Assert.Equal(1, latest.Count);
        Assert.Equal(newer, Assert.Single(latest.Values));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryUpdate_EqualOrOlderTimestamp_PreservesValue(int ticks)
    {
        var latest = new LatestByKey<VehiclePosition>(position => position.MeasuredAt);
        var current = TestData.Position("current", "vehicle", TestData.Now);
        var incoming = TestData.Position("incoming", "vehicle", TestData.Now.AddTicks(ticks));
        latest.TryUpdate("vehicle", current);

        bool updated = latest.TryUpdate("vehicle", incoming);

        Assert.False(updated);
        Assert.Equal(1, latest.Count);
        Assert.Equal(current, Assert.Single(latest.Values));
    }

    [Fact]
    public void RemoveOlderThan_AtBoundary_KeepsBoundaryAndNewerValuesAndReturnsRemovedKey()
    {
        var latest = new LatestByKey<DateTimeOffset>(timestamp => timestamp);
        DateTimeOffset cutoff = TestData.Now;
        latest.TryUpdate("expired", cutoff.AddTicks(-1));
        latest.TryUpdate("boundary", cutoff);
        latest.TryUpdate("newer", cutoff.AddTicks(1));

        IReadOnlyList<string> removals = latest.RemoveOlderThan(cutoff);

        Assert.Equal("expired", Assert.Single(removals));
        Assert.Equal(2, latest.Count);
        Assert.Contains(cutoff, latest.Values);
        Assert.Contains(cutoff.AddTicks(1), latest.Values);
        Assert.Empty(latest.RemoveOlderThan(cutoff));
    }
}
