using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Application.Live;
using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.UnitTests.Application;

// Haritada araç başına en yeni konum bulunur; süresi dolan araç istemciden de kaldırılır.
public sealed class LiveVehicleStateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_SameVehicleWithTwoEntities_ReturnsSingleNewestUpsert(bool newestFirst)
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        var older = TestData.Position("entity-1", "vehicle", TestData.Now.AddMinutes(-1));
        var newer = TestData.Position("entity-2", "vehicle", TestData.Now);
        VehiclePosition[] positions = newestFirst ? [newer, older] : [older, newer];

        var update = state.Apply(positions, TestData.Now);

        Assert.Equal(newer, Assert.Single(update.Upserts));
        Assert.Empty(update.Removals);
        Assert.Equal(newer, Assert.Single(state.GetSnapshot().Vehicles));
    }

    [Fact]
    public void Apply_OlderPosition_IgnoresPositionAndPreservesVersion()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        var newer = TestData.Position("entity-1", "vehicle", TestData.Now);
        var older = TestData.Position("entity-2", "vehicle", TestData.Now.AddMinutes(-1));
        var initial = state.Apply([newer], TestData.Now);

        var update = state.Apply([older], TestData.Now);

        Assert.True(update.IsEmpty);
        Assert.Equal(initial.Version, update.Version);
        Assert.Equal(initial.Version, state.GetSnapshot().Version);
        Assert.Equal(newer, Assert.Single(state.GetSnapshot().Vehicles));
    }

    [Fact]
    public void Apply_ChangedPositions_IncrementsVersionAndUpdatesSnapshot()
    {
        var state = new LiveVehicleState(TestData.StorageOptions);
        var first = TestData.Position("entity-1", "vehicle-1", TestData.Now);
        var second = TestData.Position("entity-2", "vehicle-2", TestData.Now);
        long initialVersion = state.GetSnapshot().Version;
        var firstUpdate = state.Apply([first], TestData.Now);

        var secondUpdate = state.Apply([second], TestData.Now);
        var snapshot = state.GetSnapshot();

        Assert.Equal(initialVersion + 1, firstUpdate.Version);
        Assert.Equal(firstUpdate.Version + 1, secondUpdate.Version);
        Assert.Equal(secondUpdate.Version, snapshot.Version);
        Assert.Equal(2, snapshot.Vehicles.Count);
        Assert.Contains(first, snapshot.Vehicles);
        Assert.Contains(second, snapshot.Vehicles);
    }

    [Fact]
    public void Apply_PositionExpires_ReturnsRemovalAndRemovesFromSnapshot()
    {
        var clock = new FakeTimeProvider(TestData.Now);
        var state = new LiveVehicleState(TestData.StorageOptions);
        var position = TestData.Position("entity", "vehicle", clock.GetUtcNow());
        var initial = state.Apply([position], clock.GetUtcNow());
        clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromSeconds(1));

        var update = state.Apply([], clock.GetUtcNow());

        Assert.Equal("vehicle", Assert.Single(update.Removals));
        Assert.Empty(update.Upserts);
        Assert.Equal(initial.Version + 1, update.Version);
        Assert.Empty(state.GetSnapshot().Vehicles);
    }
}
