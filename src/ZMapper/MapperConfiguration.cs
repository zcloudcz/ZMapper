using System.Linq.Expressions;
using ZMapper.Abstractions;
using ZMapper.Abstractions.Configuration;

namespace ZMapper;

/// <summary>
/// Main configuration class for ZMapper
/// </summary>
public class MapperConfiguration : IMapperConfiguration
{
    public readonly List<MappingDefinition> Mappings = new();

    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
    {
        var mapping = new MappingDefinition
        {
            SourceType = typeof(TSource),
            DestinationType = typeof(TDestination)
        };

        Mappings.Add(mapping);

        return new MappingExpression<TSource, TDestination>(mapping, this);
    }

    public IMapper Build()
    {
        // This will be replaced by source-generated implementation
        throw new NotImplementedException(
            "ZMapper requires source generation. Ensure ZMapper.SourceGenerator is properly configured.");
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
        // Create reverse mapping with inverted property configurations
        var reverseMapping = new MappingDefinition
        {
            SourceType = typeof(TDestination),
            DestinationType = typeof(TSource)
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
                ConverterType = memberConfig.ConverterType,
                CustomResolver = memberConfig.CustomResolver,
                IsIgnored = false
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

    public void Ignore()
    {
        _config.IsIgnored = true;
    }

    public void When(Expression<Func<TSource, bool>> condition)
    {
        _config.Condition = condition;
    }
}
