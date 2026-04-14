namespace ZMapper;

/// <summary>
/// Carries runtime parameters into mapping operations.
/// Use this to pass dynamic values that change per-mapping-call (e.g., "IgnoreNested").
///
/// For beginners: Think of this as a dictionary you pass alongside your data when mapping.
/// Conditions and converters can read values from it to change behavior at runtime.
///
/// Example usage:
///   var ctx = new MappingContext();
///   ctx["IgnoreNested"] = true;
///   var result = mapper.Map&lt;CompanyDto, Company&gt;(dto, ctx);
/// </summary>
public sealed class MappingContext
{
    private readonly Dictionary<string, object> _items = new();

    /// <summary>Gets or sets a runtime parameter by key.</summary>
    public object this[string key]
    {
        get => _items[key];
        set => _items[key] = value;
    }

    /// <summary>Tries to get a value by key. Returns false if the key is not present.</summary>
    public bool TryGetValue(string key, out object? value) => _items.TryGetValue(key, out value);

    /// <summary>Checks whether a key exists in the context.</summary>
    public bool ContainsKey(string key) => _items.ContainsKey(key);

    /// <summary>Gets a strongly-typed value by key. Throws if key not found or type mismatch.</summary>
    public T Get<T>(string key) => (T)_items[key];

    /// <summary>Gets a strongly-typed value or returns defaultValue if the key is not present.</summary>
    public T GetOrDefault<T>(string key, T defaultValue = default!)
        => _items.TryGetValue(key, out var value) ? (T)value : defaultValue;
}
