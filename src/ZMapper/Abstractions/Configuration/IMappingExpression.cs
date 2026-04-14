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
    /// Replaces the entire property-by-property mapping with a single conversion expression.
    /// When used, ForMember configurations are ignored — the lambda IS the mapping.
    ///
    /// For beginners: Instead of copying properties one by one, this lets you write
    /// a single expression that produces the destination object. Useful for simple type
    /// conversions like CreateMap&lt;ListDto, int&gt;().ConvertUsing(s => s.Id).
    /// </summary>
    IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> converter);

    /// <summary>
    /// Replaces the entire property-by-property mapping with an ITypeConverter implementation.
    /// The converter class must implement ITypeConverter&lt;TSource, TDestination&gt;.
    ///
    /// For beginners: Like ConvertUsing with a lambda, but delegates to a reusable converter class.
    /// Useful for registering global type conversions (e.g., DateTimeOffset -> DateTime).
    /// </summary>
    IMappingExpression<TSource, TDestination> ConvertUsing<TConverter>()
        where TConverter : ITypeConverter<TSource, TDestination>, new();

    /// <summary>
    /// Provides a custom factory expression for creating the destination object.
    /// After construction, normal property-by-property mapping still applies.
    ///
    /// For beginners: Use this when the destination type doesn't have a parameterless
    /// constructor, or when you need custom initialization logic before property mapping.
    /// </summary>
    IMappingExpression<TSource, TDestination> ConstructUsing(Expression<Func<TSource, TDestination>> factory);

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
    /// Uses a custom converter for this member.
    /// The source property is the same as the destination property name (convention-based).
    /// </summary>
    void ConvertUsing<TConverter>() where TConverter : IMemberConverter<TSource, TMember, TMember>;

    /// <summary>
    /// Uses a custom converter with an explicit source property.
    /// Use when source property name differs from destination.
    ///
    /// For beginners: Sometimes the source value comes from a differently-named property:
    ///   opt.ConvertUsing&lt;DateConverter&gt;(s => s.IssuanceDateOffset)
    /// This reads IssuanceDateOffset, converts it, and assigns to the destination property.
    /// </summary>
    void ConvertUsing<TConverter, TSourceMember>(
        Expression<Func<TSource, TSourceMember>> sourceMember)
        where TConverter : IMemberConverter<TSource, TSourceMember, TMember>, new();

    /// <summary>
    /// Ignores this member
    /// </summary>
    void Ignore();

    /// <summary>
    /// Conditionally maps this member based on a predicate
    /// </summary>
    void When(Expression<Func<TSource, bool>> condition);

    /// <summary>
    /// Conditionally maps this member based on a predicate with access to runtime MappingContext.
    /// Evaluated BEFORE resolving the source value — if the condition is false, source is not read.
    ///
    /// For beginners: Use this when the condition depends on runtime parameters:
    ///   opt.PreCondition((src, ctx) => ctx.GetOrDefault&lt;bool&gt;("MapNested", true))
    /// The mapping only happens if PreCondition returns true. Requires Map(source, context) overload.
    /// </summary>
    void PreCondition(Expression<Func<TSource, MappingContext, bool>> condition);
}
