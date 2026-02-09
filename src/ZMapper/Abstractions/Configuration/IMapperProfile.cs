namespace ZMapper;

/// <summary>
/// Interface for defining mapping profiles.
/// Implement this interface to group related mappings together (like AutoMapper profiles).
///
/// For beginners: A profile is a class where you define your mappings.
/// Instead of putting all mappings in one place, you can organize them by domain/feature.
///
/// Example:
/// <code>
/// public class UserProfile : IMapperProfile
/// {
///     public void Configure(MapperConfiguration config)
///     {
///         config.CreateMap&lt;UserDto, User&gt;()
///             .ForMember(d => d.UserId, o => o.MapFrom(s => s.Id));
///     }
/// }
/// </code>
/// </summary>
public interface IMapperProfile
{
    /// <summary>
    /// Configure mappings for this profile.
    /// Called automatically during DI registration (AddZMapper) or manually.
    /// </summary>
    /// <param name="config">The mapper configuration to register mappings with</param>
    void Configure(MapperConfiguration config);
}
