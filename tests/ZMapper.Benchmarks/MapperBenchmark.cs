using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace ZMapper.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public partial class MapperBenchmark
{
    private PersonDto _source = null!;
    private AutoMapper.IMapper _autoMapper = null!;
    private ZMapper.IMapper _zMapper = null!;
    private MapperlyMapper _mapperlyMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create test data
        _source = new PersonDto
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Age = 30,
            PhoneNumber = "+1-555-1234",
            Address = "123 Main St",
            City = "New York",
            Country = "USA",
            CreatedAt = DateTime.Now
        };

        // Setup AutoMapper
        var autoMapperConfig = new AutoMapper.MapperConfiguration(cfg =>
        {
            cfg.CreateMap<PersonDto, Person>()
                .ForMember(dest => dest.PersonId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.PhoneNumber))
                .ForMember(dest => dest.AddressLine, opt => opt.MapFrom(src => src.Address))
                .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country))
                .ForMember(dest => dest.RegistrationDate, opt => opt.MapFrom(src => src.CreatedAt));
        });
        _autoMapper = autoMapperConfig.CreateMapper();

        // Setup ZMapper
        _zMapper = ZMapperConfig.CreateMapper();

        // Setup Mapperly
        _mapperlyMapper = new MapperlyMapper();
    }

    [Benchmark(Baseline = true)]
    public Person ManualMapping()
    {
        return new Person
        {
            PersonId = _source.Id,
            FirstName = _source.FirstName,
            LastName = _source.LastName,
            EmailAddress = _source.Email,
            Age = _source.Age,
            Phone = _source.PhoneNumber,
            AddressLine = _source.Address,
            City = _source.City,
            CountryName = _source.Country,
            RegistrationDate = _source.CreatedAt
        };
    }

    [Benchmark]
    public Person ZMapper()
    {
        // Use extension method for zero-overhead mapping (as fast as manual!)
        return _source.ToPerson();
    }

    [Benchmark]
    public Person Mapperly()
    {
        return _mapperlyMapper.Map(_source);
    }

    [Benchmark]
    public Person AutoMapper()
    {
        return _autoMapper.Map<Person>(_source);
    }
}
