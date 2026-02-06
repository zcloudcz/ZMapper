using ZMapper.Abstractions;

namespace ZMapper.Benchmarks;

/// <summary>
/// ZMapper configuration for all benchmark mappings.
/// Registers both simple (PersonDto→Person) and complex (Order/Customer) mappings.
/// The source generator will produce optimized mapping code at compile time.
/// </summary>
public partial class ZMapperConfig
{
    /// <summary>
    /// Creates mapper for simple flat object benchmarks (PersonDto → Person).
    /// </summary>
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Simple flat mapping with renamed properties
        config.CreateMap<PersonDto, Person>()
            .ForMember(dest => dest.PersonId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.Email))
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.AddressLine, opt => opt.MapFrom(src => src.Address))
            .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country))
            .ForMember(dest => dest.RegistrationDate, opt => opt.MapFrom(src => src.CreatedAt));

        // Complex nested mappings - order matters: innermost types first
        // Address mapping (flat, but many properties including nullable)
        config.CreateMap<ComplexAddress, ComplexAddressDto>();
        // Customer mapping (nested Address objects)
        config.CreateMap<ComplexCustomer, ComplexCustomerDto>();
        // Order status mapping (enum + nullable fields)
        config.CreateMap<ComplexOrderStatusInfo, ComplexOrderStatusInfoDto>();
        // Order item mapping (TotalPrice is calculated read-only on source, writable on dest)
        config.CreateMap<ComplexOrderItem, ComplexOrderItemDto>()
            .IgnoreNonExisting();
        // Order mapping (nested status + collection of items)
        config.CreateMap<ComplexOrder, ComplexOrderDto>();

        return CreateGeneratedMapper();
    }
}
