using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for collection performance
public class ItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public partial class CollectionMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();
        config.CreateMap<ItemDto, Item>();
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for collection mapping with various sizes
/// </summary>
public class CollectionPerformanceTests
{
    private readonly IMapper _mapper;

    public CollectionPerformanceTests()
    {
        _mapper = CollectionMapperConfig.CreateMapper();
    }

    [Fact]
    public void MapArray_WithEmptyArray_ShouldReturnEmptyArray()
    {
        // Arrange
        var source = Array.Empty<ItemDto>();

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(source);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapList_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var source = new List<ItemDto>();

        // Act
        var result = _mapper.MapList<ItemDto, Item>(source);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapArray_WithSingleItem_ShouldMapCorrectly()
    {
        // Arrange
        var source = new[]
        {
            new ItemDto { Id = 1, Name = "Single", Price = 10.00m }
        };

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("Single");
        result[0].Price.Should().Be(10.00m);
    }

    [Fact]
    public void MapArray_With100Items_ShouldMapAll()
    {
        // Arrange
        var source = Enumerable.Range(1, 100)
            .Select(i => new ItemDto { Id = i, Name = $"Item {i}", Price = i * 10.00m })
            .ToArray();

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(source);

        // Assert
        result.Should().HaveCount(100);
        result[0].Id.Should().Be(1);
        result[99].Id.Should().Be(100);
        result[99].Name.Should().Be("Item 100");
        result[99].Price.Should().Be(1000.00m);
    }

    [Fact]
    public void MapArray_With1000Items_ShouldMapAll()
    {
        // Arrange
        var source = Enumerable.Range(1, 1000)
            .Select(i => new ItemDto { Id = i, Name = $"Item {i}", Price = i * 5.00m })
            .ToArray();

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(source);

        // Assert
        result.Should().HaveCount(1000);
        result[0].Id.Should().Be(1);
        result[500].Id.Should().Be(501);
        result[999].Id.Should().Be(1000);
    }

    [Fact]
    public void MapList_With100Items_ShouldMapAll()
    {
        // Arrange
        var source = Enumerable.Range(1, 100)
            .Select(i => new ItemDto { Id = i, Name = $"Item {i}", Price = i * 2.50m })
            .ToList();

        // Act
        var result = _mapper.MapList<ItemDto, Item>(source);

        // Assert
        result.Should().HaveCount(100);
        result[0].Id.Should().Be(1);
        result[50].Name.Should().Be("Item 51");
        result[99].Price.Should().Be(250.00m);
    }

    [Fact]
    public void MapList_With1000Items_ShouldMapAll()
    {
        // Arrange
        var source = Enumerable.Range(1, 1000)
            .Select(i => new ItemDto { Id = i, Name = $"Product {i}", Price = i * 1.00m })
            .ToList();

        // Act
        var result = _mapper.MapList<ItemDto, Item>(source);

        // Assert
        result.Should().HaveCount(1000);
        result.First().Id.Should().Be(1);
        result.Last().Id.Should().Be(1000);
    }

    [Fact]
    public void MapArray_WithSpan_ShouldWorkCorrectly()
    {
        // Arrange
        var source = new[]
        {
            new ItemDto { Id = 1, Name = "First", Price = 10.00m },
            new ItemDto { Id = 2, Name = "Second", Price = 20.00m },
            new ItemDto { Id = 3, Name = "Third", Price = 30.00m }
        };
        ReadOnlySpan<ItemDto> span = source;

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(span);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("First");
        result[1].Price.Should().Be(20.00m);
        result[2].Id.Should().Be(3);
    }

    [Fact]
    public void MapArray_PreservesOrder_With50Items()
    {
        // Arrange
        var source = Enumerable.Range(1, 50)
            .Select(i => new ItemDto { Id = i, Name = $"Item {i}", Price = i })
            .ToArray();

        // Act
        var result = _mapper.MapArray<ItemDto, Item>(source);

        // Assert - Verify order is preserved
        for (int i = 0; i < 50; i++)
        {
            result[i].Id.Should().Be(i + 1);
            result[i].Name.Should().Be($"Item {i + 1}");
        }
    }
}
