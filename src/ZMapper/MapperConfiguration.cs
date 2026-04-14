using System.Linq.Expressions;

namespace ZMapper;

/// <summary>
/// Main configuration class for ZMapper
/// </summary>
public class MapperConfiguration : IMapperConfiguration
{
    public readonly List<MappingDefinition> Mappings = new();

    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        return CreateMap<TSource, TDestination>(MemberList.Destination);
    }

    /// <summary>
    /// Creates a mapping between TSource and TDestination with the specified validation mode.
    /// </summary>
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(MemberList memberList)
    {
        var mapping = new MappingDefinition
        {
            SourceType = typeof(TSource),
            DestinationType = typeof(TDestination),
            MemberListValidation = memberList
        };

        Mappings.Add(mapping);

        return new MappingExpression<TSource, TDestination>(mapping, this);
    }

    /// <summary>
    /// Factory function set by the source-generated code to create a mapper from this configuration.
    /// Users call CreateMapper() which delegates to this factory.
    /// </summary>
    public static Func<MapperConfiguration, IMapper>? MapperFactory { get; set; }

    /// <summary>
    /// Creates a mapper instance from this configuration.
    /// Requires source-generated Mapper class to be available.
    ///
    /// For beginners: Use this for standalone (non-DI) scenarios:
    ///   var config = new MapperConfiguration();
    ///   new MyProfile().Configure(config);
    ///   var mapper = config.CreateMapper();
    /// </summary>
    public IMapper CreateMapper()
    {
        if (MapperFactory != null)
            return MapperFactory(this);

        throw new InvalidOperationException(
            "No MapperFactory registered. Ensure ZMapper source generator is configured " +
            "and at least one IMapperProfile exists, or use Mapper.Create(config) directly.");
    }

    [System.Obsolete("Use CreateMapper() instead.")]
    public IMapper Build()
    {
        return CreateMapper();
    }
}

internal class MappingExpression<TSource, TDestination> : IMappingExpression<TSource, TDestination>
{
    private readonly MappingDefinition _mapping;
    private readonly MapperConfiguration _configuration;

    public MappingExpression(MappingDefinition mapping, MapperConfiguration configuration)
    {
        _mapping = mapping;
        _configuration = configuration;
    }

    public IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> options)
    {
        var memberName = GetMemberName(destinationMember);
        var memberConfig = new MemberConfiguration
        {
            DestinationMemberName = memberName,
            MemberType = typeof(TMember)
        };

        var expression = new MemberConfigurationExpression<TSource, TDestination, TMember>(memberConfig);
        options(expression);

        _mapping.MemberConfigurations.Add(memberConfig);
        return this;
    }


    public IMappingExpression<TDestination, TSource> ReverseMap()
    {
        // Create reverse mapping with inverted property configurations.
        // Propagation matches the source-generator logic (see ExtractReverseMappingIfPresent):
        //  - IgnoreNonExisting and MemberListValidation are copied (shared intent)
        //  - ConvertUsing / ConstructUsing are NOT copied (direction-specific)
        //  - PreCondition on members is copied (usually context-based, safe)
        var reverseMapping = new MappingDefinition
        {
            SourceType = typeof(TDestination),
            DestinationType = typeof(TSource),
            IgnoreNonExisting = _mapping.IgnoreNonExisting,
            MemberListValidation = _mapping.MemberListValidation
        };

        // Invert each member configuration
        foreach (var memberConfig in _mapping.MemberConfigurations)
        {
            // Skip ignored members
            if (memberConfig.IsIgnored)
                continue;

            // For reverse mapping:
            // Original: dest.OrderId <- src.Id (DestinationMemberName="OrderId", SourceMemberName="Id")
            // Reverse:  dest.Id <- src.OrderId (DestinationMemberName="Id", SourceMemberName="OrderId")
            var reverseMemberConfig = new MemberConfiguration
            {
                DestinationMemberName = memberConfig.SourceMemberName ?? memberConfig.DestinationMemberName,
                SourceMemberName = memberConfig.DestinationMemberName,
                MemberType = memberConfig.MemberType,
                // ConverterType is NOT copied — IMemberConverter<,,> generic args differ per direction
                CustomResolver = memberConfig.CustomResolver,
                IsIgnored = false,
                PreCondition = memberConfig.PreCondition
            };

            reverseMapping.MemberConfigurations.Add(reverseMemberConfig);
        }

        _configuration.Mappings.Add(reverseMapping);
        return new MappingExpression<TDestination, TSource>(reverseMapping, _configuration);
    }

    // Opt out of unmapped property diagnostics. When called, destination properties
    // without a matching source property are silently skipped (no compile-time warning).
    public IMappingExpression<TSource, TDestination> IgnoreNonExisting()
    {
        _mapping.IgnoreNonExisting = true;
        return this;
    }

    // Replace entire mapping with a lambda expression (e.g., s => s.Id for ListDto -> int)
    public IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> converter)
    {
        _mapping.ConvertUsingExpression = converter;
        return this;
    }

    // Replace entire mapping with an ITypeConverter class (e.g., DateTimeOffsetToDateTime)
    public IMappingExpression<TSource, TDestination> ConvertUsing<TConverter>()
        where TConverter : ITypeConverter<TSource, TDestination>, new()
    {
        _mapping.ConvertUsingConverterType = typeof(TConverter);
        return this;
    }

    // Use a custom factory to create the destination object instead of new TDestination()
    public IMappingExpression<TSource, TDestination> ConstructUsing(Expression<Func<TSource, TDestination>> factory)
    {
        _mapping.ConstructUsingExpression = factory;
        return this;
    }

    public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action)
    {
        _mapping.BeforeMapAction = action;
        return this;
    }

    public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action)
    {
        _mapping.AfterMapAction = action;
        return this;
    }

    private static string GetMemberName<TMember>(Expression<Func<TDestination, TMember>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }
        throw new ArgumentException("Expression must be a member expression");
    }
}

internal class MemberConfigurationExpression<TSource, TDestination, TMember>
    : IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    private readonly MemberConfiguration _config;

    public MemberConfigurationExpression(MemberConfiguration config)
    {
        _config = config;
    }

    public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
    {
        if (sourceMember.Body is MemberExpression memberExpression)
        {
            _config.SourceMemberName = memberExpression.Member.Name;
        }
    }

    public void ConvertUsing<TConverter>() where TConverter : IMemberConverter<TSource, TMember, TMember>
    {
        _config.ConverterType = typeof(TConverter);
    }

    // ConvertUsing with explicit source property (e.g., ConvertUsing<Conv>(s => s.OtherProp))
    public void ConvertUsing<TConverter, TSourceMember>(
        Expression<Func<TSource, TSourceMember>> sourceMember)
        where TConverter : IMemberConverter<TSource, TSourceMember, TMember>, new()
    {
        _config.ConverterType = typeof(TConverter);
        if (sourceMember.Body is MemberExpression memberExpression)
        {
            _config.SourceMemberName = memberExpression.Member.Name;
        }
    }

    public void Ignore()
    {
        _config.IsIgnored = true;
    }

    public void When(Expression<Func<TSource, bool>> condition)
    {
        _config.Condition = condition;
    }

    // Conditional mapping with access to runtime MappingContext
    // Evaluated before resolving source value — if false, the property is skipped
    public void PreCondition(Expression<Func<TSource, MappingContext, bool>> condition)
    {
        _config.PreCondition = condition;
    }
}
