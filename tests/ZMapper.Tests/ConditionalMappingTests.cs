using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for conditional mapping
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class Product
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Active { get; set; }
}

public partial class ConditionalMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        config.CreateMap<ProductDto, Product>()
            .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Price, opt =>
            {
                opt.MapFrom(src => src.Price);
                opt.When(src => src.Price > 0);  // Only map if Price is positive
            })
            .ForMember(dest => dest.Description, opt =>
            {
                opt.MapFrom(src => src.Description!);
                opt.When(src => src.Description != null);  // Only map if Description is not null
            })
            .ForMember(dest => dest.Active, opt => opt.MapFrom(src => src.IsActive));

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for conditional mapping with When() - maps properties only when condition is met
/// </summary>
public class ConditionalMappingTests
{
    private readonly IMapper _mapper;

    public ConditionalMappingTests()
    {
        _mapper = ConditionalMapperConfig.CreateMapper();
    }

    [Fact]
    public void ConditionalMapping_WithAllValues_ShouldMapAllProperties()
    {
        // Arrange
        var dto = new ProductDto
        {
            Id = 1,
            Name = "Test Product",
            Price = 99.99m,
            Description = "Test Description",
            IsActive = true
        };

        // Act
        var product = _mapper.Map<ProductDto, Product>(dto);

        // Assert
        product.Should().NotBeNull();
        product.ProductId.Should().Be(1);
        product.ProductName.Should().Be("Test Product");
        product.Price.Should().Be(99.99m);
        product.Description.Should().Be("Test Description");
        product.Active.Should().BeTrue();
    }

    [Fact]
    public void ConditionalMapping_WithZeroPrice_ShouldNotMapPrice()
    {
        // Arrange
        var dto = new ProductDto
        {
            Id = 2,
            Name = "Product With Zero Price",
            Price = 0,  // Condition will fail (not > 0), Price should not be mapped
            Description = "Has Description",
            IsActive = false
        };

        // Act
        var product = _mapper.Map<ProductDto, Product>(dto);

        // Assert
        product.Should().NotBeNull();
        product.ProductId.Should().Be(2);
        product.ProductName.Should().Be("Product With Zero Price");
        product.Price.Should().Be(0);  // Default value, not mapped
        product.Description.Should().Be("Has Description");
        product.Active.Should().BeFalse();
    }

    [Fact]
    public void ConditionalMapping_WithNullDescription_ShouldNotMapDescription()
    {
        // Arrange
        var dto = new ProductDto
        {
            Id = 3,
            Name = "Product Without Description",
            Price = 49.99m,
            Description = null,  // Condition will fail, Description should not be mapped
            IsActive = true
        };

        // Act
        var product = _mapper.Map<ProductDto, Product>(dto);

        // Assert
        product.Should().NotBeNull();
        product.ProductId.Should().Be(3);
        product.ProductName.Should().Be("Product Without Description");
        product.Price.Should().Be(49.99m);
        product.Description.Should().Be(string.Empty);  // Default value, not mapped
        product.Active.Should().BeTrue();
    }

    [Fact]
    public void ConditionalMapping_WithBothFailingConditions_ShouldMapOnlyOtherProperties()
    {
        // Arrange
        var dto = new ProductDto
        {
            Id = 4,
            Name = "Minimal Product",
            Price = 0,          // Won't be mapped (not > 0)
            Description = null, // Won't be mapped (null)
            IsActive = true
        };

        // Act
        var product = _mapper.Map<ProductDto, Product>(dto);

        // Assert
        product.Should().NotBeNull();
        product.ProductId.Should().Be(4);
        product.ProductName.Should().Be("Minimal Product");
        product.Price.Should().Be(0);           // Default value
        product.Description.Should().Be(string.Empty);  // Default value
        product.Active.Should().BeTrue();
    }
}
