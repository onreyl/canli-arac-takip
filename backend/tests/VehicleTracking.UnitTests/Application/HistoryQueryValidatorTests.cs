using Microsoft.Extensions.Options;
using VehicleTracking.Application.History;
using VehicleTracking.Application.Storage;

namespace VehicleTracking.UnitTests.Application;

public sealed class HistoryQueryValidatorTests
{
    private static HistoryQueryValidator Create(HistoryOptions? history = null, TimeSpan? retention = null) =>
        new(Options.Create(new StorageOptions { Retention = retention ?? TimeSpan.FromHours(3) }),
            Options.Create(history ?? new HistoryOptions()));

    private static ResolvedHistoryQuery Resolve(string vehicleId, DateTimeOffset? from, DateTimeOffset? to,
        int pageSize, string? cursor, HistoryOptions? options = null, TimeSpan? retention = null) =>
        ResolvedHistoryQuery.Resolve(new(vehicleId, from, to, pageSize, cursor), TestData.Now,
            retention ?? TimeSpan.FromHours(3), options ?? new HistoryOptions());

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(600000000, true)]
    public void Validate_RetentionToleranceBoundary(long ticks, bool valid)
    {
        var query = Resolve("vehicle", TestData.Now.AddHours(-3).AddMinutes(-1).AddTicks(ticks), null, 200, null);
        var result = Create().Validate(query);
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("from", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Validate_EndBoundary(long ticks, bool valid)
    {
        var start = TestData.Now.AddMinutes(-10);
        var result = Create().Validate(Resolve("vehicle", start, start.AddTicks(ticks), 200, null));
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("to", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(17, true)]
    [InlineData(18, false)]
    public void Validate_PageSizeUsesConfiguredMaximum(int size, bool valid)
    {
        var result = Create(new HistoryOptions { MaxPageSize = 17 }).Validate(Resolve("vehicle", null, null, size, null));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal("pageSize", error.PropertyName);
            Assert.Equal("Sayfa boyutu 1 ile 17 arasında olmalı.", error.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("%%%", false)]
    public void Validate_Cursor(string? cursor, bool valid)
    {
        var result = Create().Validate(Resolve("vehicle", null, null, 200, cursor));
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Equal("cursor", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Validate_ValidEncodedCursor_IsAccepted()
    {
        string cursor = HistoryCursorCodec.Encode(new(TestData.Now, "entity"));
        Assert.True(Create().Validate(Resolve("vehicle", null, null, 200, cursor)).IsValid);
    }

    [Fact]
    public void Validate_AllRulesFail_ReturnsAllLowercaseKeysAndConfiguredRetentionMessage()
    {
        var start = TestData.Now.AddMinutes(-7);
        var result = Create(retention: TimeSpan.FromMinutes(5)).Validate(Resolve("vehicle", start, start, 0, "%%%", retention: TimeSpan.FromMinutes(5)));
        Assert.Equal(["from", "to", "pageSize", "cursor"], result.Errors.Select(error => error.PropertyName));
        Assert.Equal("Başlangıç saklama penceresinin (son 5 dakika) dışında.", result.Errors[0].ErrorMessage);
        Assert.Equal("Bitiş başlangıçtan sonra olmalı.", result.Errors[1].ErrorMessage);
        Assert.Equal("Geçersiz imleç.", result.Errors[3].ErrorMessage);
    }

    [Fact]
    public void Validate_DefaultRangeAndToleranceUseOptions()
    {
        var options = new HistoryOptions
        {
            DefaultRange = TimeSpan.FromMinutes(5), ClockSkewTolerance = TimeSpan.FromSeconds(10)
        };
        var retention = TimeSpan.FromMinutes(5);
        var validator = Create(options, retention);
        Assert.True(validator.Validate(Resolve("vehicle", null, TestData.Now.AddSeconds(-10), 200, null, options, retention)).IsValid);
        Assert.False(validator.Validate(Resolve("vehicle", null, TestData.Now.AddSeconds(-10).AddTicks(-1), 200, null, options, retention)).IsValid);
    }
}
