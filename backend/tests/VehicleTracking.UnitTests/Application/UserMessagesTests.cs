using VehicleTracking.Application.Common;

namespace VehicleTracking.UnitTests.Application;

public sealed class UserMessagesTests
{
    [Theory]
    [InlineData(180, "3 saat")]
    [InlineData(5, "5 dakika")]
    [InlineData(90, "1 saat 30 dakika")]
    [InlineData(0, "0 saniye")]
    [InlineData(1501, "1 gün 1 saat 1 dakika")]
    [InlineData(0.5, "30 saniye")]
    public void FormatDuration_UsesReadableTurkishUnits(double minutes, string expected)
    {
        Assert.Equal(expected, UserMessages.FormatDuration(TimeSpan.FromMinutes(minutes)));
    }
}
