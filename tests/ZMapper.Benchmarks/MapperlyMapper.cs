using Riok.Mapperly.Abstractions;

namespace ZMapper.Benchmarks;

[Mapper]
public partial class MapperlyMapper
{
    [MapProperty(nameof(PersonDto.Id), nameof(Person.PersonId))]
    [MapProperty(nameof(PersonDto.Email), nameof(Person.EmailAddress))]
    [MapProperty(nameof(PersonDto.PhoneNumber), nameof(Person.Phone))]
    [MapProperty(nameof(PersonDto.Address), nameof(Person.AddressLine))]
    [MapProperty(nameof(PersonDto.Country), nameof(Person.CountryName))]
    [MapProperty(nameof(PersonDto.CreatedAt), nameof(Person.RegistrationDate))]
    public partial Person Map(PersonDto source);
}
