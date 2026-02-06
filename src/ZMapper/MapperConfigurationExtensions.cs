using ZMapper.Abstractions;

namespace ZMapper;

/// <summary>
/// Extension methods for mapper configuration
/// </summary>
public static class MapperConfigurationExtensions
{
    /// <summary>
    /// Serializes configuration to JSON for source generator.
    /// Uses simple string concatenation to avoid System.Text.Json dependency on netstandard2.0.
    /// </summary>
    internal static string SerializeForSourceGenerator(this MapperConfiguration config)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var m in config.Mappings)
        {
            var memberParts = new System.Collections.Generic.List<string>();
            foreach (var mc in m.MemberConfigurations)
            {
                memberParts.Add(
                    $"{{\"DestinationMemberName\":\"{mc.DestinationMemberName}\"," +
                    $"\"SourceMemberName\":{(mc.SourceMemberName != null ? $"\"{mc.SourceMemberName}\"" : "null")}," +
                    $"\"IsIgnored\":{mc.IsIgnored.ToString().ToLowerInvariant()}," +
                    $"\"ConverterType\":{(mc.ConverterType != null ? $"\"{mc.ConverterType.FullName}\"" : "null")}}}");
            }

            parts.Add(
                $"{{\"SourceType\":\"{m.SourceType.FullName}\"," +
                $"\"DestinationType\":\"{m.DestinationType.FullName}\"," +
                $"\"Members\":[{string.Join(",", memberParts)}]}}");
        }

        return $"[{string.Join(",", parts)}]";
    }
}
