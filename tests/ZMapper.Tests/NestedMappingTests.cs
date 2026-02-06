using FluentAssertions;
using Xunit;
using ZMapper.Abstractions;

namespace ZMapper.Tests;

// Test DTOs for nested mapping tests (using N prefix to avoid conflicts with other test files)
public class NAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
}

public class NCustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NAddressDto? Address { get; set; }
}

public class NOrderDto
{
    public int OrderId { get; set; }
    public NCustomerDto? Customer { get; set; }
    public List<NOrderItemDto> Items { get; set; } = new();
}

public class NOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

// Domain models
public class NAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
}

public class NCustomer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NAddress? Address { get; set; }
}

public class NOrder
{
    public int OrderId { get; set; }
    public NCustomer? Customer { get; set; }
    public List<NOrderItem> Items { get; set; } = new();
}

public class NOrderItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

// Mapper configuration
public partial class NestedMapperConfig
{
    public static IMapper ConfigureMapper()
    {
        var config = new MapperConfiguration();

        // Configure all nested mappings
        // IMPORTANT: Order matters! Define innermost types first
        config.CreateMap<NAddressDto, NAddress>();
        config.CreateMap<NCustomerDto, NCustomer>();
        config.CreateMap<NOrderItemDto, NOrderItem>();
        config.CreateMap<NOrderDto, NOrder>();

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for nested object mapping - the #1 critical feature for real-world usage.
/// These tests verify that ZMapper can handle complex object graphs with multiple levels of nesting.
/// </summary>
public class NestedMappingTests
{
    private readonly IMapper _mapper;

    public NestedMappingTests()
    {
        _mapper = NestedMapperConfig.ConfigureMapper();
    }

    [Fact]
    public void Should_Map_Simple_Nested_Object()
    {
        // Arrange - Create a customer with an address (1 level of nesting)
        var dto = new NCustomerDto
        {
            Id = 1,
            Name = "John Doe",
            Address = new NAddressDto
            {
                Street = "123 Main St",
                City = "Springfield",
                Zip = "12345"
            }
        };

        // Act - Map the DTO to domain model
        var customer = _mapper.Map<NCustomerDto, NCustomer>(dto);

        // Assert - Verify all properties mapped correctly
        customer.Should().NotBeNull();
        customer.Id.Should().Be(1);
        customer.Name.Should().Be("John Doe");

        // CRITICAL: The nested Address object should be mapped, not null!
        customer.Address.Should().NotBeNull();
        customer.Address!.Street.Should().Be("123 Main St");
        customer.Address.City.Should().Be("Springfield");
        customer.Address.Zip.Should().Be("12345");
    }

    [Fact]
    public void Should_Map_Nested_Object_With_Null()
    {
        // Arrange - Customer without an address (null nested object)
        var dto = new NCustomerDto
        {
            Id = 2,
            Name = "Jane Smith",
            Address = null  // Null nested object should be handled gracefully
        };

        // Act
        var customer = _mapper.Map<NCustomerDto, NCustomer>(dto);

        // Assert - Null nested objects should remain null (not throw exception)
        customer.Should().NotBeNull();
        customer.Id.Should().Be(2);
        customer.Name.Should().Be("Jane Smith");
        customer.Address.Should().BeNull();  // Null in = null out
    }

    [Fact]
    public void Should_Map_Two_Levels_Deep()
    {
        // Arrange - Order → Customer → Address (2 levels of nesting)
        var dto = new NOrderDto
        {
            OrderId = 100,
            Customer = new NCustomerDto
            {
                Id = 1,
                Name = "John Doe",
                Address = new NAddressDto
                {
                    Street = "456 Oak Ave",
                    City = "Portland",
                    Zip = "97201"
                }
            }
        };

        // Act
        var order = _mapper.Map<NOrderDto, NOrder>(dto);

        // Assert - Verify the entire object graph mapped correctly
        order.Should().NotBeNull();
        order.OrderId.Should().Be(100);

        // First level: Customer
        order.Customer.Should().NotBeNull();
        order.Customer!.Id.Should().Be(1);
        order.Customer.Name.Should().Be("John Doe");

        // Second level: Address within Customer
        order.Customer.Address.Should().NotBeNull();
        order.Customer.Address!.Street.Should().Be("456 Oak Ave");
        order.Customer.Address.City.Should().Be("Portland");
        order.Customer.Address.Zip.Should().Be("97201");
    }

    [Fact]
    public void Should_Map_Collection_Of_Nested_Objects()
    {
        // Arrange - Order with multiple items (collection of complex objects)
        var dto = new NOrderDto
        {
            OrderId = 200,
            Items = new List<NOrderItemDto>
            {
                new() { ProductId = 10, Quantity = 2 },
                new() { ProductId = 20, Quantity = 5 },
                new() { ProductId = 30, Quantity = 1 }
            }
        };

        // Act
        var order = _mapper.Map<NOrderDto, NOrder>(dto);

        // Assert - Collection should be mapped with all elements
        order.Items.Should().NotBeNull();
        order.Items.Should().HaveCount(3);
        order.Items[0].ProductId.Should().Be(10);
        order.Items[0].Quantity.Should().Be(2);
        order.Items[1].ProductId.Should().Be(20);
        order.Items[1].Quantity.Should().Be(5);
        order.Items[2].ProductId.Should().Be(30);
        order.Items[2].Quantity.Should().Be(1);
    }

    [Fact]
    public void Should_Map_Complex_Object_With_All_Features()
    {
        // Arrange - The "kitchen sink" test: nested objects + collections
        var dto = new NOrderDto
        {
            OrderId = 300,
            Customer = new NCustomerDto
            {
                Id = 5,
                Name = "Alice Johnson",
                Address = new NAddressDto
                {
                    Street = "789 Pine Rd",
                    City = "Seattle",
                    Zip = "98101"
                }
            },
            Items = new List<NOrderItemDto>
            {
                new() { ProductId = 100, Quantity = 3 },
                new() { ProductId = 200, Quantity = 1 }
            }
        };

        // Act
        var order = _mapper.Map<NOrderDto, NOrder>(dto);

        // Assert - Comprehensive validation of the entire object graph
        order.OrderId.Should().Be(300);

        // Nested customer
        order.Customer.Should().NotBeNull();
        order.Customer!.Id.Should().Be(5);
        order.Customer.Name.Should().Be("Alice Johnson");

        // Nested address within customer
        order.Customer.Address.Should().NotBeNull();
        order.Customer.Address!.Street.Should().Be("789 Pine Rd");
        order.Customer.Address.City.Should().Be("Seattle");
        order.Customer.Address.Zip.Should().Be("98101");

        // Collection of items
        order.Items.Should().HaveCount(2);
        order.Items[0].ProductId.Should().Be(100);
        order.Items[0].Quantity.Should().Be(3);
        order.Items[1].ProductId.Should().Be(200);
        order.Items[1].Quantity.Should().Be(1);
    }

    [Fact]
    public void Should_Use_Extension_Method_For_Nested_Mapping()
    {
        // Arrange - Test the generated .ToNCustomer() extension method
        var dto = new NCustomerDto
        {
            Id = 10,
            Name = "Extension Test",
            Address = new NAddressDto
            {
                Street = "Extension St",
                City = "Test City",
                Zip = "99999"
            }
        };

        // Act - Use extension method instead of mapper.Map<>()
        var customer = dto.ToNCustomer();

        // Assert - Extension methods should work identically to mapper.Map<>()
        customer.Should().NotBeNull();
        customer.Id.Should().Be(10);
        customer.Name.Should().Be("Extension Test");
        customer.Address.Should().NotBeNull();
        customer.Address!.Street.Should().Be("Extension St");
        customer.Address.City.Should().Be("Test City");
    }
}
