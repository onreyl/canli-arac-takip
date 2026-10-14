using VehicleTracking.Domain.Vehicles;

namespace VehicleTracking.UnitTests.Domain;

// Boş sefer kimliğinin sınırlarını ve bitmiş seferleri eleyen saklama penceresini korur.
public sealed class VehiclePositionTests
{
    [Theory]
    [InlineData("BOR:DeadRun:260907141040052_0", true)]
    [InlineData("BRA:ServiceJourney:6660_1022", false)]
    [InlineData(null, false)]
    [InlineData("BORDeadRun260907141040052_0", false)]
    public void IsDeadRun_TripIdVariants_ReturnsExpectedResult(string? tripId, bool expected)
    {
        // Ayraçsız DeadRun metni sefer türünü kanıtlamaz; :DeadRun: aranmasının nedeni budur.
        VehiclePosition position = TestData.Position("entity", "vehicle", TestData.Now, tripId);

        bool result = position.IsDeadRun;

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsWithinRetention_AtOrBeforeBoundary_ReturnsExpectedResult(int secondsBeforeBoundary, bool expected)
    {
        VehiclePosition position = TestData.Position("entity", "vehicle",
            TestData.Now.AddHours(-3).AddSeconds(-secondsBeforeBoundary));

        bool result = position.IsWithinRetention(TestData.Now, TestData.StorageOptions.Value.Retention);

        Assert.Equal(expected, result);
    }
}
