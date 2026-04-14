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

    /// <summary>
    /// When set, the entire mapping is replaced by a ConvertUsing expression or converter type.
    /// Property-by-property mapping is skipped.
    /// </summary>
    public object? ConvertUsingExpression { get; set; } // Expression<Func<TSource, TDestination>>

    /// <summary>
    /// The type of ITypeConverter to use for whole-object conversion (ConvertUsing&lt;T&gt;()).
    /// When set, the generator emits new T().Convert(source) instead of property mapping.
    /// </summary>
    public Type? ConvertUsingConverterType { get; set; }

    /// <summary>
    /// When set, the destination object is created using this factory expression
    /// instead of new TDestination(). Property mapping still applies after construction.
    /// </summary>
    public object? ConstructUsingExpression { get; set; } // Expression<Func<TSource, TDestination>>

    /// <summary>
    /// Controls which side of the mapping is validated for property coverage.
    /// Default is MemberList.Destination (all dest properties must have a source).
    /// </summary>
    public MemberList MemberListValidation { get; set; } = MemberList.Destination;
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
    public object? PreCondition { get; set; } // Expression<Func<TSource, MappingContext, bool>>
}
