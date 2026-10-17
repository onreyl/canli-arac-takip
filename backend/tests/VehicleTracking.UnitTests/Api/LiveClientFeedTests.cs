using VehicleTracking.Api.Realtime;
using VehicleTracking.Application.Live;

namespace VehicleTracking.UnitTests.Api;

// Yavaş istemci ara konumları biriktirmez; her aracın son değişikliğini alır.
// İlk tam görüntü, yeniden bağlanan istemcinin güncel duruma dönmesini sağlar.
public sealed class LiveClientFeedTests
{
    [Fact]
    public void TakePending_NewFeed_ReturnsFullSnapshot()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        var first = TestData.Position("entity-1", "vehicle-1", TestData.Now);
        var second = TestData.Position("entity-2", "vehicle-2", TestData.Now);
        state.Apply([first, second], TestData.Now);
        using var feed = new LiveClientFeed();

        var result = feed.TakePending(state, out string method);

        Assert.Equal("snapshot", method);
        var snapshot = Assert.IsType<LiveSnapshotMessage>(result);
        Assert.Equal(state.GetSnapshot().Version, snapshot.Version);
        Assert.Equal(2, snapshot.Vehicles.Count);
        Assert.Contains(LiveVehicleDto.From(first), snapshot.Vehicles);
        Assert.Contains(LiveVehicleDto.From(second), snapshot.Vehicles);
    }

    [Fact]
    public void Merge_TwoPositionsForSameVehicle_ReturnsOnlyLatestUpsert()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        using var feed = new LiveClientFeed();
        feed.TakePending(state, out _);
        var older = TestData.Position("entity", "vehicle", TestData.Now.AddSeconds(-3));
        var newer = TestData.Position("entity", "vehicle", TestData.Now);

        feed.Merge(new LiveUpdate(1, [older], []));
        feed.Merge(new LiveUpdate(2, [newer], []));
        var result = feed.TakePending(state, out string method);

        Assert.Equal("update", method);
        var update = Assert.IsType<LiveUpdateMessage>(result);
        Assert.Equal(LiveVehicleDto.From(newer), Assert.Single(update.Upserts));
        Assert.Empty(update.Removals);
    }

    [Fact]
    public void Merge_UpsertThenRemoval_ReturnsOnlyRemoval()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        using var feed = new LiveClientFeed();
        feed.TakePending(state, out _);
        var position = TestData.Position("entity", "vehicle", TestData.Now);

        feed.Merge(new LiveUpdate(1, [position], []));
        feed.Merge(new LiveUpdate(2, [], ["vehicle"]));
        var result = feed.TakePending(state, out _);

        var update = Assert.IsType<LiveUpdateMessage>(result);
        Assert.Empty(update.Upserts);
        Assert.Equal("vehicle", Assert.Single(update.Removals));
    }

    [Fact]
    public void Merge_RemovalThenUpsert_ReturnsOnlyUpsert()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        using var feed = new LiveClientFeed();
        feed.TakePending(state, out _);
        var position = TestData.Position("entity", "vehicle", TestData.Now);

        feed.Merge(new LiveUpdate(1, [], ["vehicle"]));
        feed.Merge(new LiveUpdate(2, [position], []));
        var result = feed.TakePending(state, out _);

        var update = Assert.IsType<LiveUpdateMessage>(result);
        Assert.Equal(LiveVehicleDto.From(position), Assert.Single(update.Upserts));
        Assert.Empty(update.Removals);
    }

    [Fact]
    public void TakePending_AfterReadingChanges_ReturnsEmptyChanges()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        using var feed = new LiveClientFeed();
        feed.TakePending(state, out _);
        var position = TestData.Position("entity", "vehicle", TestData.Now);
        feed.Merge(new LiveUpdate(1, [position], ["removed-vehicle"]));
        var first = Assert.IsType<LiveUpdateMessage>(feed.TakePending(state, out _));

        var result = feed.TakePending(state, out string method);

        Assert.Single(first.Upserts);
        Assert.Single(first.Removals);
        Assert.Equal("update", method);
        var update = Assert.IsType<LiveUpdateMessage>(result);
        Assert.Empty(update.Upserts);
        Assert.Empty(update.Removals);
    }

    [Fact]
    public void Merge_MultipleVersions_ReturnsLastMergedVersion()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        using var feed = new LiveClientFeed();
        feed.TakePending(state, out _);
        var position = TestData.Position("entity", "vehicle", TestData.Now);

        feed.Merge(new LiveUpdate(3, [position], []));
        feed.Merge(new LiveUpdate(7, [], ["vehicle"]));
        var result = feed.TakePending(state, out _);

        Assert.Equal(7, Assert.IsType<LiveUpdateMessage>(result).Version);
    }
}
