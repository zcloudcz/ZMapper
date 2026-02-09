using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace ZMapper.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public partial class BatchMapperBenchmark
{
    private PersonDto[] _sources = null!;
    private AutoMapper.IMapper _autoMapper = null!;
    private ZMapper.IMapper _zMapper = null!;
    private MapperlyMapper _mapperlyMapper = null!;

    [Params(10, 100, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Create test data
        _sources = Enumerable.Range(1, Count).Select(i => new PersonDto
        {
            Id = i,
            FirstName = $"John{i}",
            LastName = $"Doe{i}",
            Email = $"john.doe{i}@example.com",
            Age = 20 + (i % 50),
            PhoneNumber = $"+1-555-{i:D4}",
            Address = $"{i} Main St",
            City = "New York",
            Country = "USA",
            CreatedAt = DateTime.Now.AddDays(-i)
        }).ToArray();

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
    public Person[] ManualMapping()
    {
        var result = new Person[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            var src = _sources[i];
            result[i] = new Person
            {
                PersonId = src.Id,
                FirstName = src.FirstName,
                LastName = src.LastName,
                EmailAddress = src.Email,
                Age = src.Age,
                Phone = src.PhoneNumber,
                AddressLine = src.Address,
                City = src.City,
                CountryName = src.Country,
                RegistrationDate = src.CreatedAt
            };
        }
        return result;
    }

    [Benchmark]
    public Person[] ZMapperWithSpan()
    {
        return _zMapper.MapArray<PersonDto, Person>(_sources.AsSpan());
    }

    [Benchmark]
    public Person[] ZMapperLoop()
    {
        var result = new Person[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            result[i] = _zMapper.Map<PersonDto, Person>(_sources[i]);
        }
        return result;
    }

    [Benchmark]
    public Person[] MapperlyLoop()
    {
        var result = new Person[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            result[i] = _mapperlyMapper.Map(_sources[i]);
        }
        return result;
    }

    [Benchmark]
    public List<Person> AutoMapperList()
    {
        return _autoMapper.Map<List<Person>>(_sources);
    }

    [Benchmark]
    public Person[] AutoMapperLoop()
    {
        var result = new Person[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
        {
            result[i] = _autoMapper.Map<Person>(_sources[i]);
        }
        return result;
    }
}
