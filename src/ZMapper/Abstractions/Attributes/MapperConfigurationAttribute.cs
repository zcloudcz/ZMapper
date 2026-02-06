using System;

namespace ZMapper.Abstractions.Attributes;

/// <summary>
/// Marks a class as mapper configuration for source generation
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class MapperConfigurationAttribute : Attribute
{
    public string? GeneratedClassName { get; set; }
}
