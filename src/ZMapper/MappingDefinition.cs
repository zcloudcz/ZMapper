namespace ZMapper;

/// <summary>
/// Internal representation of a mapping configuration
/// </summary>
public class MappingDefinition
{
    public Type SourceType { get; set; } = null!;
    public Type DestinationType { get; set; } = null!;
    public List<MemberConfiguration> MemberConfigurations { get; set; } = new();
    public object? BeforeMapAction { get; set; } // Action<TSource, TDestination>
    public object? AfterMapAction { get; set; } // Action<TSource, TDestination>
    public bool IgnoreNonExisting { get; set; }
}

/// <summary>
/// Configuration for a single member mapping
/// </summary>
public class MemberConfiguration
{
    public string DestinationMemberName { get; set; } = null!;
    public string? SourceMemberName { get; set; }
    public Type? MemberType { get; set; }
    public Type? ConverterType { get; set; }
    public object? CustomResolver { get; set; }
    public bool IsIgnored { get; set; }
    public object? Condition { get; set; } // Expression<Func<TSource, bool>>
}
