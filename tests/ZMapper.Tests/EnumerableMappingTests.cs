using FluentAssertions;
using ZMapper.Abstractions;
using Xunit;

namespace ZMapper.Tests;

// DTOs for enumerable mapping tests (prefixed to avoid name conflicts)
public class EnumTestProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class EnumTestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>
/// Mapper configuration for enumerable tests.
/// Uses the existing CreateMap pattern (backward compatible).
/// </summary>
public partial class EnumerableMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();
        config.CreateMap<EnumTestProductDto, EnumTestProduct>();
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for IEnumerable&lt;T&gt; MapList overload.
///
/// These tests verify that the new MapList(IEnumerable&lt;T&gt;) overload
/// works correctly with various collection types: HashSet, IEnumerable
/// from LINQ, ICollection, and plain arrays cast to IEnumerable.
/// </summary>
public class EnumerableMappingTests
{
    private readonly IMapper _mapper;

    public EnumerableMappingTests()
    {
        _mapper = EnumerableMapperConfig.CreateMapper();
    }

    [Fact]
    public void MapList_WithIEnumerable_ShouldMapAllItems()
    {
        // Arrange - IEnumerable from LINQ .Where()
        var source = new List<EnumTestProductDto>
        {
            new() { Id = 1, Name = "Widget", Price = 9.99m },
            new() { Id = 2, Name = "Gadget", Price = 19.99m },
            new() { Id = 3, Name = "Thingamajig", Price = 29.99m }
        };
        IEnumerable<EnumTestProductDto> filtered = source.Where(p => p.Price > 10m);

        // Act
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>(filtered);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Gadget");
        result[1].Name.Should().Be("Thingamajig");
    }

    [Fact]
    public void MapList_WithHashSet_ShouldMapAllItems()
    {
        // Arrange - HashSet implements IEnumerable but not IReadOnlyList
        var source = new HashSet<EnumTestProductDto>
        {
            new() { Id = 1, Name = "Alpha", Price = 10m },
            new() { Id = 2, Name = "Beta", Price = 20m }
        };

        // Act
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>((IEnumerable<EnumTestProductDto>)source);

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().BeEquivalentTo(new[] { "Alpha", "Beta" });
    }

    [Fact]
    public void MapList_WithEmptyEnumerable_ShouldReturnEmptyList()
    {
        // Arrange
        IEnumerable<EnumTestProductDto> empty = Enumerable.Empty<EnumTestProductDto>();

        // Act
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>(empty);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapList_WithYieldEnumerable_ShouldMapAllItems()
    {
        // Arrange - Test with yield return (lazy enumerable)
        static IEnumerable<EnumTestProductDto> Generate()
        {
            yield return new EnumTestProductDto { Id = 1, Name = "First", Price = 1m };
            yield return new EnumTestProductDto { Id = 2, Name = "Second", Price = 2m };
            yield return new EnumTestProductDto { Id = 3, Name = "Third", Price = 3m };
        }

        // Act
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>(Generate());

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
        result[2].Id.Should().Be(3);
    }

    [Fact]
    public void MapList_WithSelectProjection_ShouldMapAllItems()
    {
        // Arrange - IEnumerable from LINQ .Select() projection
        var ids = new[] { 1, 2, 3 };
        IEnumerable<EnumTestProductDto> projected = ids.Select(id =>
            new EnumTestProductDto { Id = id, Name = $"Product-{id}", Price = id * 10m });

        // Act
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>(projected);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Product-1");
        result[0].Price.Should().Be(10m);
        result[2].Name.Should().Be("Product-3");
        result[2].Price.Should().Be(30m);
    }

    [Fact]
    public void MapList_IReadOnlyList_StillWorks()
    {
        // Arrange - Verify the original IReadOnlyList overload still works
        var source = new List<EnumTestProductDto>
        {
            new() { Id = 1, Name = "Original", Price = 5m }
        };

        // Act - List<T> implements IReadOnlyList<T>
        var result = _mapper.MapList<EnumTestProductDto, EnumTestProduct>(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Original");
    }
}
