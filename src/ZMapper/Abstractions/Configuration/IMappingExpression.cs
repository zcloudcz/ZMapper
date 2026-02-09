using System;
using System.Linq.Expressions;

namespace ZMapper;

/// <summary>
/// Fluent interface for configuring mappings
/// </summary>
public interface IMappingExpression<TSource, TDestination>
{
    /// <summary>
    /// Configures a custom mapping for a destination member
    /// </summary>
    IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> options);

    /// <summary>
    /// Reverses the mapping (creates TDestination -> TSource mapping)
    /// </summary>
    IMappingExpression<TDestination, TSource> ReverseMap();

    /// <summary>
    /// Suppresses compile-time warnings (ZMAP001) for destination properties that have no matching source property.
    /// Without this, ZMapper emits a warning for every unmapped destination property.
    ///
    /// For beginners: By default, ZMapper warns you when your destination has properties
    /// that don't exist on the source. Call IgnoreNonExisting() to say "I know, and that's intentional"
    /// - those properties will keep their default values (null, 0, false, etc.).
    /// This is especially useful when hooks (BeforeMap/AfterMap) populate destination-only properties.
    /// </summary>
    IMappingExpression<TSource, TDestination> IgnoreNonExisting();

    /// <summary>
    /// Executes custom logic before mapping
    /// </summary>
    IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action);

    /// <summary>
    /// Executes custom logic after mapping
    /// </summary>
    IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action);
}

/// <summary>
/// Configuration expression for a member
/// </summary>
public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    /// <summary>
    /// Maps from a source member
    /// </summary>
    void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember);

    /// <summary>
    /// Uses a custom converter
    /// </summary>
    void ConvertUsing<TConverter>() where TConverter : IMemberConverter<TSource, TMember, TMember>;

    /// <summary>
    /// Ignores this member
    /// </summary>
    void Ignore();

    /// <summary>
    /// Conditionally maps this member based on a predicate
    /// </summary>
    void When(Expression<Func<TSource, bool>> condition);
}
