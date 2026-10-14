using System.Buffers.Text;
using System.Text;
using VehicleTracking.Api.Controllers;
using VehicleTracking.Application.History;

namespace VehicleTracking.UnitTests.Api;

// Geçmiş sayfalamasında zaman ve varlık kimliği birlikte korunmalı; bozuk imleç kabul edilmemeli.
public sealed class HistoryCursorCodecTests
{
    [Fact]
    public void Encode_RealisticEntityId_RoundTripsCursor()
    {
        var cursor = new HistoryCursor(TestData.Now.AddMilliseconds(123),
            "BRA:ServiceJourney:6660_1022-20261014-258010350");

        string encoded = HistoryCursorCodec.Encode(cursor);
        bool success = HistoryCursorCodec.TryDecode(encoded, out var decoded);

        Assert.True(success);
        Assert.Equal(cursor, decoded);
    }

    [Theory]
    [InlineData("%%%")]
    [InlineData("")]
    public void TryDecode_InvalidText_ReturnsFalse(string value)
    {
        bool success = HistoryCursorCodec.TryDecode(value, out var cursor);

        Assert.False(success);
        Assert.Null(cursor);
    }

    [Fact]
    public void TryDecode_EncodedTextWithoutSeparator_ReturnsFalse()
    {
        string encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("1791979200000"));

        bool success = HistoryCursorCodec.TryDecode(encoded, out var cursor);

        Assert.False(success);
        Assert.Null(cursor);
    }
}
