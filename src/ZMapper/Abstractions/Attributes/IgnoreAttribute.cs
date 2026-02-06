using System;

namespace ZMapper.Abstractions.Attributes;

/// <summary>
/// Marks a property to be ignored during mapping
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IgnoreAttribute : Attribute
{
}
