using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for ConvertUsing lambda (Feature 1)
// ============================================================================

/// <summary>A DTO that wraps an Id and Text — used for simple type extraction.</summary>
public class ListDto
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>Source with composite name fields — tests expression-based ConvertUsing.</summary>
public class FullNameSource
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

/// <summary>Destination that holds a single concatenated name.</summary>
public class FullNameResult
{
    public string FullName { get; set; } = string.Empty;
}

// ============================================================================
// Test models for ConvertUsing<TConverter> (Feature 2)
// ============================================================================

/// <summary>Converts DateTimeOffset to DateTime (UTC).</summary>
public class DateTimeOffsetToDateTimeConverter : ITypeConverter<DateTimeOffset, DateTime>
{
    public DateTime Convert(DateTimeOffset source)
    {
        return source.UtcDateTime;
    }
}

/// <summary>Converts DateTime to DateTimeOffset (UTC).</summary>
public class DateTimeToDateTimeOffsetConverter : ITypeConverter<DateTime, DateTimeOffset>
{
    public DateTimeOffset Convert(DateTime source)
    {
        return new DateTimeOffset(source, TimeSpan.Zero);
    }
}

/// <summary>Converts a string to its uppercase form.</summary>
public class UpperCaseConverter : ITypeConverter<string, string>
{
    public string Convert(string source)
    {
        return source.ToUpperInvariant();
    }
}

/// <summary>Source with DateTimeOffset property — tests auto-applied type converter.</summary>
public class EventSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Destination with DateTime property — converter auto-applies to CreatedAt.</summary>
public class EventDestination
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ============================================================================
// Test models for ConstructUsing (Feature 3)
// ============================================================================

/// <summary>A class with no parameterless constructor — must use ConstructUsing.</summary>
public class ImmutablePerson
{
    public int Id { get; }
    public string Name { get; }
    public int Age { get; set; } // Mutable property — mapped after construction

    public ImmutablePerson(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

/// <summary>Source DTO for ImmutablePerson.</summary>
public class PersonInputDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

// ============================================================================
// Mapper configuration for all ConvertUsing and ConstructUsing tests
// ============================================================================
public partial class ConvertUsingMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Feature 1: ConvertUsing lambda — extract Id from ListDto
        config.CreateMap<ListDto, int>().ConvertUsing(s => s.Id);

        // Feature 1: ConvertUsing lambda — extract Text from ListDto
        config.CreateMap<ListDto, string>().ConvertUsing(s => s.Text);

        // Feature 2: ConvertUsing<TConverter> — whole-type conversion
        config.CreateMap<DateTimeOffset, DateTime>().ConvertUsing<DateTimeOffsetToDateTimeConverter>();
        config.CreateMap<DateTime, DateTimeOffset>().ConvertUsing<DateTimeToDateTimeOffsetConverter>();

        // Feature 3: ConstructUsing — custom factory, then property mapping continues
        config.CreateMap<PersonInputDto, ImmutablePerson>()
            .ConstructUsing(s => new ImmutablePerson(s.Id, s.Name))
            .IgnoreNonExisting(); // Id and Name are read-only, no setter

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================
public class ConvertUsingTests
{
    private readonly IMapper _mapper;

    public ConvertUsingTests()
    {
        _mapper = ConvertUsingMapperConfig.CreateMapper();
    }

    // --- Feature 1: ConvertUsing lambda ---

    [Fact]
    public void ConvertUsing_Lambda_ExtractInt_ShouldWork()
    {
        // Arrange
        var source = new ListDto { Id = 42, Text = "Hello" };

        // Act
        var result = _mapper.Map<ListDto, int>(source);

        // Assert — entire mapping replaced by s => s.Id
        result.Should().Be(42);
    }

    [Fact]
    public void ConvertUsing_Lambda_ExtractString_ShouldWork()
    {
        // Arrange
        var source = new ListDto { Id = 1, Text = "World" };

        // Act
        var result = _mapper.Map<ListDto, string>(source);

        // Assert
        result.Should().Be("World");
    }

    [Fact]
    public void ConvertUsing_Lambda_ViaGenericDispatch_ShouldWork()
    {
        // Arrange — extension methods are NOT generated for built-in destination types
        // (Int32, String, etc.) to avoid shadowing Object.ToString() / Convert.ToInt32().
        // Users should use mapper.Map<S,D>() for these mappings.
        var source = new ListDto { Id = 99, Text = "Extension" };

        // Act
        var resultInt = _mapper.Map<ListDto, int>(source);
        var resultStr = _mapper.Map<ListDto, string>(source);

        // Assert
        resultInt.Should().Be(99);
        resultStr.Should().Be("Extension");
    }

    // --- Feature 2: ConvertUsing<TConverter> ---

    [Fact]
    public void ConvertUsing_TypeConverter_DateTimeOffsetToDateTime_ShouldWork()
    {
        // Arrange
        var offset = new DateTimeOffset(2026, 3, 15, 10, 30, 0, TimeSpan.FromHours(2));

        // Act
        var result = _mapper.Map<DateTimeOffset, DateTime>(offset);

        // Assert — should be UTC
        result.Should().Be(new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ConvertUsing_TypeConverter_DateTimeToDateTimeOffset_ShouldWork()
    {
        // Arrange
        var dt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = _mapper.Map<DateTime, DateTimeOffset>(dt);

        // Assert
        result.Should().Be(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ConvertUsing_TypeConverter_ExtensionMethod_ShouldWork()
    {
        // Arrange
        var offset = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act — extension method
        var result = offset.ToDateTime();

        // Assert
        result.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    // --- Feature 3: ConstructUsing ---

    [Fact]
    public void ConstructUsing_WithParameterizedConstructor_ShouldWork()
    {
        // Arrange
        var dto = new PersonInputDto { Id = 7, Name = "Alice", Age = 30 };

        // Act
        var result = _mapper.Map<PersonInputDto, ImmutablePerson>(dto);

        // Assert — Id and Name set via constructor, Age set via property mapping
        result.Id.Should().Be(7);
        result.Name.Should().Be("Alice");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void ConstructUsing_ExtensionMethod_ShouldWork()
    {
        // Arrange
        var dto = new PersonInputDto { Id = 3, Name = "Bob", Age = 25 };

        // Act
        var result = dto.ToImmutablePerson();

        // Assert
        result.Id.Should().Be(3);
        result.Name.Should().Be("Bob");
        result.Age.Should().Be(25);
    }
}
