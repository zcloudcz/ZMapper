using ZMapper.Abstractions.Configuration;

namespace ZMapper.Example;

/// <summary>
/// Profile for User-related mappings.
/// Demonstrates ForMember and Ignore configuration within a profile.
/// </summary>
public class UserMappingProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        // Simple mapping with property name differences
        config.CreateMap<UserDto, User>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.RegisteredDate, opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.Active, opt => opt.MapFrom(src => src.IsActive))
            .ForMember(dest => dest.FullName, opt => opt.Ignore());
    }
}

/// <summary>
/// Profile for Address-related mappings.
/// </summary>
public class AddressMappingProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<AddressDto, Address>()
            .ForMember(dest => dest.StreetAddress, opt => opt.MapFrom(src => src.Street))
            .ForMember(dest => dest.Zip, opt => opt.MapFrom(src => src.PostalCode))
            .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country));
    }
}

/// <summary>
/// Profile for Customer-related mappings.
/// </summary>
public class CustomerMappingProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<CustomerDto, Customer>()
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.CustomerName))
            .ForMember(dest => dest.ContactEmail, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Address, opt => opt.Ignore())
            .ForMember(dest => dest.Labels, opt => opt.Ignore());
    }
}
