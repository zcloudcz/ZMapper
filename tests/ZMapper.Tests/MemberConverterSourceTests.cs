using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for ConvertUsing + source property (Feature 6)
// ============================================================================

/// <summary>Converter that formats a DateTime as dd/MM/yyyy string.</summary>
public class ToDDMMYYYYConverter : IMemberConverter<DateSourceDto, DateTime, string>
{
    public string Convert(DateSourceDto source, DateTime sourceMember)
    {
        return sourceMember.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Converter that doubles an int value.</summary>
public class DoubleValueConverter : IMemberConverter<ScoreSourceDto, int, int>
{
    public int Convert(ScoreSourceDto source, int sourceMember)
    {
        return sourceMember * 2;
    }
}

public class DateSourceDto
{
    public int Id { get; set; }
    public DateTime IssuanceDateOffset { get; set; }
    public DateTime CreationDate { get; set; }
}

public class DateDestDto
{
    public int Id { get; set; }
    public string IssuanceDate { get; set; } = string.Empty;
    public string CreatedOn { get; set; } = string.Empty;
}

public class ScoreSourceDto
{
    public int Id { get; set; }
    public int RawScore { get; set; }
}

public class ScoreDestDto
{
    public int Id { get; set; }
    public int FinalScore { get; set; }
}

// ============================================================================
// Mapper configuration
// ============================================================================
public partial class MemberConverterSourceConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // ConvertUsing with explicit source property — source name differs from dest
        config.CreateMap<DateSourceDto, DateDestDto>()
            .ForMember(d => d.IssuanceDate,
                opt => opt.ConvertUsing<ToDDMMYYYYConverter, DateTime>(s => s.IssuanceDateOffset))
            .ForMember(d => d.CreatedOn,
                opt => opt.ConvertUsing<ToDDMMYYYYConverter, DateTime>(s => s.CreationDate));

        // ConvertUsing with explicit source property — different property name
        config.CreateMap<ScoreSourceDto, ScoreDestDto>()
            .ForMember(d => d.FinalScore,
                opt => opt.ConvertUsing<DoubleValueConverter, int>(s => s.RawScore));

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================
public class MemberConverterSourceTests
{
    private readonly IMapper _mapper;

    public MemberConverterSourceTests()
    {
        _mapper = MemberConverterSourceConfig.CreateMapper();
    }

    [Fact]
    public void ConvertUsing_WithExplicitSourceProp_ShouldConvertCorrectly()
    {
        // Arrange
        var source = new DateSourceDto
        {
            Id = 1,
            IssuanceDateOffset = new DateTime(2026, 3, 15),
            CreationDate = new DateTime(2026, 1, 1)
        };

        // Act
        var result = _mapper.Map<DateSourceDto, DateDestDto>(source);

        // Assert — converter reads from IssuanceDateOffset, writes to IssuanceDate
        result.Id.Should().Be(1);
        result.IssuanceDate.Should().Be("15/03/2026");
        result.CreatedOn.Should().Be("01/01/2026");
    }

    [Fact]
    public void ConvertUsing_WithExplicitSourceProp_ExtensionMethod_ShouldWork()
    {
        // Arrange
        var source = new DateSourceDto
        {
            Id = 2,
            IssuanceDateOffset = new DateTime(2026, 12, 25),
            CreationDate = new DateTime(2026, 6, 15)
        };

        // Act — extension method
        var result = source.ToDateDestDto();

        // Assert
        result.IssuanceDate.Should().Be("25/12/2026");
        result.CreatedOn.Should().Be("15/06/2026");
    }

    [Fact]
    public void ConvertUsing_DoubleConverter_ShouldApplyTransformation()
    {
        // Arrange
        var source = new ScoreSourceDto { Id = 5, RawScore = 42 };

        // Act
        var result = _mapper.Map<ScoreSourceDto, ScoreDestDto>(source);

        // Assert — RawScore * 2 = FinalScore
        result.Id.Should().Be(5);
        result.FinalScore.Should().Be(84);
    }
}
