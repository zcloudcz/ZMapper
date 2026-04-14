using System;
using System.Collections.Generic;

namespace ZMapper;

/// <summary>
/// Main interface for object mapping.
/// Provides methods for mapping single objects, arrays (via Span), and lists.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// Maps source object to destination type
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>
    /// Maps source object to destination type with runtime context parameters.
    /// Context values are accessible in PreCondition and When expressions.
    ///
    /// For beginners: Pass runtime parameters like "IgnoreNested" that change mapping behavior:
    ///   var ctx = new MappingContext();
    ///   ctx["IgnoreNested"] = true;
    ///   mapper.Map&lt;CompanyDto, Company&gt;(dto, ctx);
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, MappingContext context);

    /// <summary>
    /// Maps source object to existing destination instance
    /// </summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>
    /// Maps collection of source objects to destination type collection using high-performance Span
    /// </summary>
    TDestination[] MapArray<TSource, TDestination>(ReadOnlySpan<TSource> source);

    /// <summary>
    /// Maps list of source objects to destination type list (accepts IReadOnlyList for indexed access)
    /// </summary>
    List<TDestination> MapList<TSource, TDestination>(IReadOnlyList<TSource> source);

    /// <summary>
    /// Maps any enumerable of source objects to destination type list.
    /// Use this overload for IEnumerable, ICollection, HashSet, EF query results, etc.
    /// </summary>
    List<TDestination> MapList<TSource, TDestination>(IEnumerable<TSource> source);
}
