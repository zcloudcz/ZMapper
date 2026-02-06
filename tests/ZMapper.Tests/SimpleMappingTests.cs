using FluentAssertions;
using ZMapper.Abstractions;
using Xunit;

namespace ZMapper.Tests;

// Simple test classes
public class SimpleSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class SimpleDestination
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class CustomSource
{
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
}

public class CustomDestination
{
    public int DestId { get; set; }
    public string DestName { get; set; } = string.Empty;
}

public partial class SimpleTestMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Convention-based mapping
        config.CreateMap<SimpleSource, SimpleDestination>();

        // Custom property mapping
        config.CreateMap<CustomSource, CustomDestination>()
            .ForMember(dest => dest.DestId, opt => opt.MapFrom(src => src.SourceId))
            .ForMember(dest => dest.DestName, opt => opt.MapFrom(src => src.SourceName));

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Basic tests for simple mapping scenarios
/// </summary>
public class SimpleMappingTests
{
    private readonly IMapper _mapper;

    public SimpleMappingTests()
    {
        _mapper = SimpleTestMapperConfig.CreateMapper();
    }

    [Fact]
    public void ConventionBasedMapping_ShouldWork()
    {
        // Arrange
        var source = new SimpleSource
        {
            Id = 42,
            Name = "Test",
            Value = 100
        };

        // Act
        var dest = _mapper.Map<SimpleSource, SimpleDestination>(source);

        // Assert
        dest.Should().NotBeNull();
        dest.Id.Should().Be(42);
        dest.Name.Should().Be("Test");
        dest.Value.Should().Be(100);
    }

    [Fact]
    public void CustomPropertyMapping_ShouldWork()
    {
        // Arrange
        var source = new CustomSource
        {
            SourceId = 999,
            SourceName = "Custom"
        };

        // Act
        var dest = _mapper.Map<CustomSource, CustomDestination>(source);

        // Assert
        dest.Should().NotBeNull();
        dest.DestId.Should().Be(999);
        dest.DestName.Should().Be("Custom");
    }

    [Fact]
    public void MapArray_ShouldWork()
    {
        // Arrange
        var sources = new[]
        {
            new SimpleSource { Id = 1, Name = "First", Value = 10 },
            new SimpleSource { Id = 2, Name = "Second", Value = 20 },
            new SimpleSource { Id = 3, Name = "Third", Value = 30 }
        };

        // Act
        var destinations = _mapper.MapArray<SimpleSource, SimpleDestination>(sources);

        // Assert
        destinations.Should().HaveCount(3);
        destinations[0].Id.Should().Be(1);
        destinations[1].Name.Should().Be("Second");
        destinations[2].Value.Should().Be(30);
    }

    [Fact]
    public void MapList_ShouldWork()
    {
        // Arrange
        var sources = new List<SimpleSource>
        {
            new() { Id = 10, Name = "A", Value = 100 },
            new() { Id = 20, Name = "B", Value = 200 }
        };

        // Act
        var destinations = _mapper.MapList<SimpleSource, SimpleDestination>(sources);

        // Assert
        destinations.Should().HaveCount(2);
        destinations[0].Value.Should().Be(100);
        destinations[1].Name.Should().Be("B");
    }

}
