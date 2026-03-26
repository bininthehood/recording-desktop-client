using System.Text.Json;

public class JsonUtil
{
    private readonly Dictionary<string, object?> _dict;

    public JsonUtil(JsonElement element)
    {
        _dict = new();

        foreach (var prop in element.EnumerateObject())
        {
            var val = prop.Value;

            object? parsed = val.ValueKind switch
            {
                JsonValueKind.String => val.GetString(),
                JsonValueKind.Number => val.TryGetInt64(out var i) ? i : val.GetDouble(),
                JsonValueKind.True or JsonValueKind.False => val.GetBoolean(),
                JsonValueKind.Null => null,
                JsonValueKind.Object => new JsonUtil(val),
                JsonValueKind.Array => val.EnumerateArray().ToList(),
                _ => val.ToString()
            };

            _dict[prop.Name] = parsed;
        }
    }

    public T? Get<T>(string key)
    {
        if (_dict.TryGetValue(key, out var value))
        {
            if (value == null)
                return default;

            if (value is T tVal)
                return tVal;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    public T GetOrDefault<T>(string key, T fallback)
    {
        var val = Get<T>(key);
        return val == null ? fallback : val;
    }

    public JsonUtil? GetObject(string key)
    {
        if (_dict.TryGetValue(key, out var value) && value is JsonUtil obj)
        {
            return obj;
        }
        return null;
    }

    public List<T>? GetArray<T>(string key)
    {
        if (_dict.TryGetValue(key, out var value) && value is List<JsonElement> arr)
        {
            try
            {
                return arr.Select(x => JsonSerializer.Deserialize<T>(x.GetRawText())!).ToList();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public void Set(string key, object? value)
    {
        _dict[key] = value;
    }

    public bool Contains(string key)
    {
        return _dict.ContainsKey(key);
    }

    public void Remove(string key)
    {
        _dict.Remove(key);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(_dict, new JsonSerializerOptions { WriteIndented = true });
    }
}
