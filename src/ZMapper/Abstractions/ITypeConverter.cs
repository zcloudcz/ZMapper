namespace ZMapper;

/// <summary>
/// Interface for custom type converters
/// </summary>
public interface ITypeConverter<TSource, TDestination>
{
    TDestination Convert(TSource source);
}

/// <summary>
/// Interface for custom member converters
/// </summary>
public interface IMemberConverter<TSource, TSourceMember, TDestinationMember>
{
    TDestinationMember Convert(TSource source, TSourceMember sourceMember);
}
