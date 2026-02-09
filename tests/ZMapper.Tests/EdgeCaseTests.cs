using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for edge cases
public class EdgeSource
{
    public int IntValue { get; set; }
    public string StringValue { get; set; } = string.Empty;
    public decimal DecimalValue { get; set; }
}

public class EdgeDestination
{
    public int IntValue { get; set; }
    public string StringValue { get; set; } = string.Empty;
    public decimal DecimalValue { get; set; }
}

public partial class EdgeCaseMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();
        config.CreateMap<EdgeSource, EdgeDestination>();
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for edge cases and boundary conditions
/// </summary>
public class EdgeCaseTests
{
    private readonly IMapper _mapper;

    public EdgeCaseTests()
    {
        _mapper = EdgeCaseMapperConfig.CreateMapper();
    }

    [Fact]
    public void Mapping_WithZeroValues_ShouldWork()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = 0,
            StringValue = string.Empty,
            DecimalValue = 0.0m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.IntValue.Should().Be(0);
        dest.StringValue.Should().Be(string.Empty);
        dest.DecimalValue.Should().Be(0.0m);
    }

    [Fact]
    public void Mapping_WithMaxIntValue_ShouldPreserveValue()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = int.MaxValue,
            StringValue = "Max",
            DecimalValue = 999999.99m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.IntValue.Should().Be(int.MaxValue);
        dest.StringValue.Should().Be("Max");
    }

    [Fact]
    public void Mapping_WithMinIntValue_ShouldPreserveValue()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = int.MinValue,
            StringValue = "Min",
            DecimalValue = -999999.99m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.IntValue.Should().Be(int.MinValue);
    }

    [Fact]
    public void Mapping_WithEmptyString_ShouldPreserveEmptyString()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = 1,
            StringValue = string.Empty,
            DecimalValue = 1.0m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.StringValue.Should().Be(string.Empty);
        dest.StringValue.Should().NotBeNull();
    }

    [Fact]
    public void Mapping_WithWhitespaceString_ShouldPreserveWhitespace()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = 1,
            StringValue = "   ",
            DecimalValue = 1.0m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.StringValue.Should().Be("   ");
    }

    [Fact]
    public void Mapping_WithUnicodeCharacters_ShouldPreserveText()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = 1,
            StringValue = "Hello ä¸–ç•Ś đźŚŤ Đ—Đ´Ń€Đ°Đ˛ŃŃ‚Đ˛ŃĐą Ů…Ř±Ř­Ř¨Ř§",
            DecimalValue = 1.0m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.StringValue.Should().Be("Hello ä¸–ç•Ś đźŚŤ Đ—Đ´Ń€Đ°Đ˛ŃŃ‚Đ˛ŃĐą Ů…Ř±Ř­Ř¨Ř§");
    }

    [Fact]
    public void Mapping_WithVeryLongString_ShouldWork()
    {
        // Arrange
        var longString = new string('A', 10000); // 10k characters
        var source = new EdgeSource
        {
            IntValue = 42,
            StringValue = longString,
            DecimalValue = 123.45m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.StringValue.Should().HaveLength(10000);
        dest.StringValue.Should().Be(longString);
    }

    [Fact]
    public void Mapping_WithNegativeNumbers_ShouldWork()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = -12345,
            StringValue = "Negative",
            DecimalValue = -678.90m
        };

        // Act
        var dest = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert
        dest.IntValue.Should().Be(-12345);
        dest.DecimalValue.Should().Be(-678.90m);
    }

    [Fact]
    public void Mapping_MultipleTimesWithSameSource_ShouldCreateDifferentInstances()
    {
        // Arrange
        var source = new EdgeSource
        {
            IntValue = 42,
            StringValue = "Test",
            DecimalValue = 99.99m
        };

        // Act
        var dest1 = _mapper.Map<EdgeSource, EdgeDestination>(source);
        var dest2 = _mapper.Map<EdgeSource, EdgeDestination>(source);

        // Assert - Different instances
        dest1.Should().NotBeSameAs(dest2);

        // Assert - But same values
        dest1.IntValue.Should().Be(dest2.IntValue);
        dest1.StringValue.Should().Be(dest2.StringValue);
        dest1.DecimalValue.Should().Be(dest2.DecimalValue);
    }
}
