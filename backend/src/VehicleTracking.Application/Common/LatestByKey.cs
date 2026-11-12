namespace VehicleTracking.Application.Common;

// Aynı kural varlık bazında ön elemede ve araç bazında canlı durumda kullanılır.
/// <summary>Anahtar başına en yeni değeri tutar; eşzamanlılık çağıranın sorumluluğundadır.</summary>
public sealed class LatestByKey<TValue>(Func<TValue, DateTimeOffset> timestampOf)
{
    private readonly Dictionary<string, TValue> _values = new();

    /// <summary>Tutulan anahtar sayısı.</summary>
    public int Count => _values.Count;

    /// <summary>Tutulan en yeni değerler.</summary>
    public IReadOnlyCollection<TValue> Values => _values.Values;

    /// <summary>Anahtar yoksa veya ölçüm daha yeniyse değeri günceller.</summary>
    public bool TryUpdate(string key, TValue value)
    {
        if (_values.TryGetValue(key, out var current) && timestampOf(value) <= timestampOf(current))
        {
            return false;
        }

        _values[key] = value;
        return true;
    }

    /// <summary>Sınırdan eski değerleri siler ve silinen anahtarları döndürür.</summary>
    public IReadOnlyList<string> RemoveOlderThan(DateTimeOffset cutoff)
    {
        List<string> removals = _values
            .Where(kv => timestampOf(kv.Value) < cutoff)
            .Select(kv => kv.Key)
            .ToList();
        foreach (string key in removals)
        {
            _values.Remove(key);
        }

        return removals;
    }
}
