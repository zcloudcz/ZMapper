using System;

namespace ZMapper.Abstractions.Attributes;

/// <summary>
/// Marks a property to be mapped from a specific source property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class MapFromAttribute : Attribute
{
    public string SourcePropertyName { get; }

    public MapFromAttribute(string sourcePropertyName)
    {
        SourcePropertyName = sourcePropertyName;
    }
}
