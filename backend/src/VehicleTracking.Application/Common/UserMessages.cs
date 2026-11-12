using System.Globalization;

namespace VehicleTracking.Application.Common;

/// <summary>Kullanıcıya gösterilen doğrulama ve kullanılabilirlik mesajları.</summary>
public static class UserMessages
{
    public const string EndMustBeAfterStart = "Bitiş başlangıçtan sonra olmalı.";
    public const string InvalidCursor = "Geçersiz imleç.";
    public const string DatabaseUnavailableTitle = "Veritabanına şu an ulaşılamıyor";
    public const string DatabaseUnavailableDetail = "Geçmiş verisi geçici olarak kullanılamıyor; lütfen biraz sonra tekrar deneyin.";

    /// <summary>Saklama süresine uygun doğrulama mesajını üretir.</summary>
    public static string OutsideRetention(TimeSpan retention) =>
        $"Başlangıç saklama penceresinin (son {FormatDuration(retention)}) dışında.";

    /// <summary>Sayfa boyutu sınırını açıklar.</summary>
    public static string PageSizeOutOfRange(int max) => $"Sayfa boyutu 1 ile {max} arasında olmalı.";

    /// <summary>Süreyi Türkçe birimlerle gösterir.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        List<string> parts = [];
        if (duration.Days != 0) parts.Add($"{duration.Days} gün");
        if (duration.Hours != 0) parts.Add($"{duration.Hours} saat");
        if (duration.Minutes != 0) parts.Add($"{duration.Minutes} dakika");
        decimal seconds = (duration.Ticks % TimeSpan.TicksPerMinute) / (decimal)TimeSpan.TicksPerSecond;
        if (seconds != 0 || parts.Count == 0)
            parts.Add($"{seconds.ToString("0.#######", CultureInfo.GetCultureInfo("tr-TR"))} saniye");
        return string.Join(" ", parts);
    }
}
