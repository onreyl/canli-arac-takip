using System.Buffers.Text;
using System.Globalization;
using System.Text;
using VehicleTracking.Application.History;

namespace VehicleTracking.Api.Controllers;

/// <summary>
/// İmleci istemciye opak bir metin olarak veriyoruz (base64url). İstemci içini yorumlamaz, sadece geri gönderir;
/// böylece ileride imlecin içeriğini değiştirsek de API sözleşmesi bozulmaz.
/// </summary>
internal static class HistoryCursorCodec
{
    public static string Encode(HistoryCursor cursor)
    {
        string raw = $"{cursor.MeasuredAt.ToUnixTimeMilliseconds()}|{cursor.EntityId}";
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string value, out HistoryCursor? cursor)
    {
        cursor = null;
        try
        {
            string raw = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
            int separator = raw.IndexOf('|');
            if (separator <= 0
                || !long.TryParse(raw.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out long ms))
            {
                return false;
            }

            cursor = new HistoryCursor(DateTimeOffset.FromUnixTimeMilliseconds(ms), raw[(separator + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
