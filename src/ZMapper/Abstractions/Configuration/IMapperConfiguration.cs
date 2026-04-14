namespace ZMapper;

/// <summary>
/// Interface for mapper configuration
/// </summary>
public interface IMapperConfiguration
{
    /// <summary>
    /// Creates a mapping between source and destination types
    /// </summary>
    IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();

    /// <summary>
    /// Creates a mapping with explicit member list validation mode.
    /// </summary>
    IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList);

    /// <summary>
    /// Creates a mapper instance from this configuration (standalone, no DI needed).
    /// </summary>
    IMapper CreateMapper();

    /// <summary>
    /// Builds and returns the configured mapper (obsolete, use CreateMapper instead).
    /// </summary>
    [System.Obsolete("Use CreateMapper() instead.")]
    IMapper Build();
}
