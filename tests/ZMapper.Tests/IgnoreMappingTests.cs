using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for Ignore functionality
public class SourceWithSecret
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Secret { get; set; } = "TopSecret";
    public string InternalData { get; set; } = "Internal";
}

public class DestinationWithDefaults
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Secret { get; set; } = "DefaultSecret";
    public string InternalData { get; set; } = "DefaultInternal";
}

// Test classes for IgnoreNonExisting - source and destination have different property sets
public class PartialSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // Note: no Address or Phone properties
}

public class FullDestination
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = "DefaultAddress";
    public string Phone { get; set; } = "DefaultPhone";
}

public partial class IgnoreMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        config.CreateMap<SourceWithSecret, DestinationWithDefaults>()
            .ForMember(dest => dest.Secret, opt => opt.Ignore())
            .ForMember(dest => dest.InternalData, opt => opt.Ignore());

        // IgnoreNonExisting: source has fewer properties than destination.
        // Non-matching destination properties (Address, Phone) keep their default values.
        config.CreateMap<PartialSourceDto, FullDestination>()
            .IgnoreNonExisting();

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for Ignore() functionality - properties that should not be mapped
/// </summary>
public class IgnoreMappingTests
{
    private readonly IMapper _mapper;

    public IgnoreMappingTests()
    {
        _mapper = IgnoreMapperConfig.CreateMapper();
    }

    [Fact]
    public void IgnoredProperties_ShouldKeepDefaultValues()
    {
        // Arrange
        var source = new SourceWithSecret
        {
            Id = 100,
            Name = "Test Item",
            Secret = "ShouldNotBeCopied",
            InternalData = "ShouldAlsoNotBeCopied"
        };

        // Act
        var destination = _mapper.Map<SourceWithSecret, DestinationWithDefaults>(source);

        // Assert
        destination.Should().NotBeNull();
        destination.Id.Should().Be(100);
        destination.Name.Should().Be("Test Item");

        // These should retain their default values, not mapped from source
        destination.Secret.Should().Be("DefaultSecret");
        destination.InternalData.Should().Be("DefaultInternal");
    }

    [Fact]
    public void NonIgnoredProperties_ShouldBeMappedCorrectly()
    {
        // Arrange
        var source = new SourceWithSecret
        {
            Id = 42,
            Name = "Non-ignored Value",
            Secret = "Ignored",
            InternalData = "Also Ignored"
        };

        // Act
        var destination = _mapper.Map<SourceWithSecret, DestinationWithDefaults>(source);

        // Assert - Only Id and Name should be mapped
        destination.Id.Should().Be(42);
        destination.Name.Should().Be("Non-ignored Value");
    }

    [Fact]
    public void MultipleIgnoredProperties_ShouldAllKeepDefaults()
    {
        // Arrange
        var source = new SourceWithSecret
        {
            Id = 1,
            Name = "Test",
            Secret = "Secret1",
            InternalData = "Internal1"
        };

        // Act
        var destination = _mapper.Map<SourceWithSecret, DestinationWithDefaults>(source);

        // Assert - Both ignored properties keep defaults
        destination.Secret.Should().Be("DefaultSecret");
        destination.InternalData.Should().Be("DefaultInternal");
    }

    [Fact]
    public void IgnoreNonExisting_ShouldMapMatchingProperties()
    {
        // Arrange - source has fewer properties than destination
        var source = new PartialSourceDto { Id = 42, Name = "Partial" };

        // Act
        var destination = _mapper.Map<PartialSourceDto, FullDestination>(source);

        // Assert - matching properties should be mapped
        destination.Id.Should().Be(42);
        destination.Name.Should().Be("Partial");
    }

    [Fact]
    public void IgnoreNonExisting_NonMatchingProperties_ShouldKeepDefaults()
    {
        // Arrange - source has no Address or Phone properties
        var source = new PartialSourceDto { Id = 1, Name = "Test" };

        // Act
        var destination = _mapper.Map<PartialSourceDto, FullDestination>(source);

        // Assert - non-matching properties keep their default values
        destination.Address.Should().Be("DefaultAddress");
        destination.Phone.Should().Be("DefaultPhone");
    }
}
