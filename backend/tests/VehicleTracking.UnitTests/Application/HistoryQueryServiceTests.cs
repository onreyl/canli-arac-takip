using System.Buffers.Text;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using VehicleTracking.Application.History;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.UnitTests.Fakes;

namespace VehicleTracking.UnitTests.Application;

public sealed class HistoryQueryServiceTests
{
    private static HistoryQueryService Create(FakeVehicleHistoryReader reader, HistoryOptions? options = null,
        FakeTimeProvider? clock = null)
    {
        var historyOptions = Options.Create(options ?? new HistoryOptions());
        return new(new HistoryQueryValidator(TestData.StorageOptions, historyOptions), reader,
            clock ?? new FakeTimeProvider(TestData.Now), TestData.StorageOptions, historyOptions);
    }

    [Theory]
    [InlineData(-30, 0)]
    [InlineData(0, 0)]
    [InlineData(30, 30)]
    public async Task QueryAsync_ClipsOnlyStartBeforeRetention(int offsetSeconds, int expectedSeconds)
    {
        var reader = new FakeVehicleHistoryReader();
        var boundary = TestData.Now.AddHours(-3);
        var result = await Create(reader).QueryAsync(new("vehicle", boundary.AddSeconds(offsetSeconds), null, 200, null), default);
        Assert.Null(result.Errors);
        Assert.Equal(boundary.AddSeconds(expectedSeconds), reader.From);
        Assert.Equal(TestData.Now, reader.To);
    }

    [Fact]
    public async Task QueryAsync_DecodesCursorMapsAllFieldsAndEncodesNext()
    {
        var after = new HistoryCursor(TestData.Now.AddMinutes(-1), "previous");
        var next = new HistoryCursor(TestData.Now.AddMinutes(-2), "next");
        var position = new VehiclePosition("entity", "vehicle", "trip", "route", 59.9f, 10.7f,
            45f, 12f, "stop", VehicleStopStatus.StoppedAt, OccupancyStatus.FewSeatsAvailable, next.MeasuredAt);
        var reader = new FakeVehicleHistoryReader { Page = new([position], next) };
        using var cancellation = new CancellationTokenSource();
        var result = await Create(reader).QueryAsync(new("vehicle", null, null, 17, HistoryCursorCodec.Encode(after)), cancellation.Token);
        Assert.Null(result.Errors);
        Assert.NotNull(result.Page);
        Assert.Equal(new HistoryPointDto(next.MeasuredAt, 59.9f, 10.7f, 45f, 12f, "route", "trip", "stop",
            VehicleStopStatus.StoppedAt, OccupancyStatus.FewSeatsAvailable), Assert.Single(result.Page.Items));
        Assert.Equal(after, reader.After);
        Assert.Equal("vehicle", reader.VehicleId);
        Assert.Equal(17, reader.PageSize);
        Assert.Equal(cancellation.Token, reader.CancellationToken);
        Assert.Equal(HistoryCursorCodec.Encode(next), result.Page.NextCursor);
    }

    [Fact]
    public async Task QueryAsync_DefaultWindowUsesSameInstantAsValidation()
    {
        var clock = new FakeTimeProvider(TestData.Now) { AutoAdvanceAmount = TimeSpan.FromSeconds(1) };
        var reader = new FakeVehicleHistoryReader();
        var result = await Create(reader, clock: clock).QueryAsync(new("vehicle", null, null, 200, null), default);
        Assert.Equal(TestData.Now.AddHours(-1), reader.From);
        Assert.Equal(TestData.Now, reader.To);
        Assert.NotNull(result.Page);
        Assert.Empty(result.Page.Items);
        Assert.Null(result.Page.NextCursor);
        Assert.Null(reader.After);
        Assert.Equal(TestData.Now.AddSeconds(1), clock.GetUtcNow());
    }

    [Fact]
    public async Task QueryAsync_ExplicitEndAndConfiguredRangeResolveStart()
    {
        var reader = new FakeVehicleHistoryReader();
        var end = TestData.Now.AddMinutes(-10);
        await Create(reader, new HistoryOptions { DefaultRange = TimeSpan.FromMinutes(20) })
            .QueryAsync(new("vehicle", null, end, 200, null), default);
        Assert.Equal(end.AddMinutes(-20), reader.From);
        Assert.Equal(end, reader.To);
    }

    [Fact]
    public async Task QueryAsync_OutOfRangeCursor_DoesNotRead()
    {
        var reader = new FakeVehicleHistoryReader();
        string cursor = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("253402300800000|entity"));

        var result = await Create(reader).QueryAsync(new("vehicle", null, null, 200, cursor), default);

        Assert.Null(result.Page);
        Assert.NotNull(result.Errors);
        Assert.Equal("cursor", Assert.Single(result.Errors).Key);
        Assert.Equal("Geçersiz imleç.", Assert.Single(result.Errors["cursor"]));
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task QueryAsync_InvalidQuery_DoesNotRead()
    {
        var reader = new FakeVehicleHistoryReader();
        var result = await Create(reader).QueryAsync(new("vehicle", TestData.Now.AddHours(-4), TestData.Now.AddHours(-4), 0, "%%%"), default);
        Assert.Null(result.Page);
        Assert.NotNull(result.Errors);
        Assert.Equal(["from", "to", "pageSize", "cursor"], result.Errors.Keys);
        Assert.Equal(0, reader.CallCount);
    }
}
