namespace ZMapper;

/// <summary>
/// Controls which side of a mapping is validated for property coverage at compile-time.
///
/// For beginners: This tells ZMapper which properties to check for missing mappings:
/// - Destination (default): Every destination property must have a matching source property.
/// - Source: Every source property must be used in the mapping.
/// - None: No validation — unmapped properties on either side are silently ignored.
/// </summary>
public enum MemberList
{
    /// <summary>Validate that all destination properties have a source. This is the default.</summary>
    Destination = 0,

    /// <summary>Validate that all source properties are used in the mapping.</summary>
    Source = 1,

    /// <summary>No validation — suppress all unmapped property diagnostics.</summary>
    None = 2
}
