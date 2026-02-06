namespace ZMapper.Abstractions.Configuration;

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
    /// Builds and returns the configured mapper
    /// </summary>
    IMapper Build();
}
