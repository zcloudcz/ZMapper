using FluentAssertions;
using ZMapper.Abstractions;
using ZMapper.Tests.Model;
using Xunit;

// Type aliases to avoid namespace conflicts with ZMapper.Tests.Order (defined in ReverseMappingTests.cs)
// C# namespace resolution prefers types from parent namespace (ZMapper.Tests.Order)
// over types from using directives (ZMapper.Tests.Model.Order)
using ModelOrder = ZMapper.Tests.Model.Order;
using ModelOrderItem = ZMapper.Tests.Model.OrderItem;

namespace ZMapper.Tests.Complex;

// === DTOs for mapping tests ===

public class AddressDto
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public double Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsDefault { get; set; }
}

public class CustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public AddressDto BillingAddress { get; set; } = new();
    public AddressDto? ShippingAddress { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public bool IsGift { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public OrderStatusInfo CurrentStatus { get; set; } = new();
    public ICollection<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
}

// === Mapper Configuration ===

public partial class ComplexTypeMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        config.CreateMap<Address, AddressDto>();
        config.CreateMap<Customer, CustomerDto>();
        // Use ModelOrderItem and ModelOrder aliases to avoid conflict with
        // ZMapper.Tests.Order/OrderItem types defined in other test files
        config.CreateMap<ModelOrderItem, OrderItemDto>();
        config.CreateMap<ModelOrder, OrderDto>();

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for complex object mapping with nested types, collections, and various data types
/// </summary>
public class ComplexTypeTests
{
    private readonly IMapper _mapper;

    public ComplexTypeTests()
    {
        _mapper = ComplexTypeMapperConfig.CreateMapper();
    }

    #region Address Tests

    [Fact]
    public void Map_Address_ShouldMapAllProperties()
    {
        // Arrange
        var address = new Address
        {
            Id = 1,
            Street = "123 Main St",
            City = "New York",
            ZipCode = "10001",
            Country = "USA",
            State = "NY",
            Latitude = 40.7128,
            Longitude = -74.0060,
            IsDefault = true
        };

        // Act
        var result = _mapper.Map<Address, AddressDto>(address);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Street.Should().Be("123 Main St");
        result.City.Should().Be("New York");
        result.ZipCode.Should().Be("10001");
        result.Country.Should().Be("USA");
        result.State.Should().Be("NY");
        result.Latitude.Should().Be(40.7128);
        result.Longitude.Should().Be(-74.0060);
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Map_Address_WithNullablePropertiesNull_ShouldMapCorrectly()
    {
        // Arrange
        var address = new Address
        {
            Id = 2,
            Street = "456 Oak Ave",
            City = "Los Angeles",
            ZipCode = "90001",
            Country = "USA",
            State = null,
            Longitude = null,
            IsDefault = false
        };

        // Act
        var result = _mapper.Map<Address, AddressDto>(address);

        // Assert
        result.State.Should().BeNull();
        result.Longitude.Should().BeNull();
    }

    #endregion

    #region Customer Tests

    [Fact]
    public void Map_Customer_WithNestedAddress_ShouldMapCorrectly()
    {
        // Arrange
        var customer = CreateTestCustomer();

        // Act
        var result = _mapper.Map<Customer, CustomerDto>(customer);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(customer.Id);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Email.Should().Be("john.doe@example.com");
        result.Gender.Should().Be(Gender.Male);
        result.LoyaltyPoints.Should().Be(500);
        result.IsActive.Should().BeTrue();

        result.BillingAddress.Should().NotBeNull();
        result.BillingAddress.City.Should().Be("Chicago");
    }

    [Fact]
    public void Map_Customer_WithNullShippingAddress_ShouldMapCorrectly()
    {
        // Arrange
        var customer = CreateTestCustomer();
        customer.ShippingAddress = null;

        // Act
        var result = _mapper.Map<Customer, CustomerDto>(customer);

        // Assert
        result.ShippingAddress.Should().BeNull();
    }

    [Fact]
    public void Map_Customer_WithDifferentShippingAddress_ShouldMapBothAddresses()
    {
        // Arrange
        var customer = CreateTestCustomer();
        customer.ShippingAddress = new Address
        {
            Id = 3,
            Street = "789 Shipping Lane",
            City = "Boston",
            ZipCode = "02101",
            Country = "USA",
            IsDefault = false
        };

        // Act
        var result = _mapper.Map<Customer, CustomerDto>(customer);

        // Assert
        result.BillingAddress.City.Should().Be("Chicago");
        result.ShippingAddress.Should().NotBeNull();
        result.ShippingAddress!.City.Should().Be("Boston");
    }

    #endregion

    #region Order Tests

    [Fact]
    public void Map_Order_WithItems_ShouldMapCorrectly()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        var result = _mapper.Map<ModelOrder, OrderDto>(order);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.OrderNumber.Should().Be("ORD-2024-001");
        result.TotalAmount.Should().Be(299.99m);
        result.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
        result.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void Map_Order_WithMultipleItems_ShouldMapAllItems()
    {
        // Arrange
        var order = CreateTestOrder();
        order.Items = new List<ModelOrderItem>
        {
            new() { Id = 1, ProductName = "Widget", Quantity = 2, UnitPrice = 49.99m },
            new() { Id = 2, ProductName = "Gadget", Quantity = 1, UnitPrice = 99.99m, IsGift = true },
            new() { Id = 3, ProductName = "Doodad", Quantity = 5, UnitPrice = 9.99m, Discount = 5.00m }
        };

        // Act
        var result = _mapper.Map<ModelOrder, OrderDto>(order);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().Contain(i => i.ProductName == "Widget" && i.Quantity == 2);
        result.Items.Should().Contain(i => i.ProductName == "Gadget" && i.IsGift);
        result.Items.Should().Contain(i => i.ProductName == "Doodad" && i.Discount == 5.00m);
    }

    [Fact]
    public void Map_Order_WithEmptyItems_ShouldMapEmptyCollection()
    {
        // Arrange
        var order = CreateTestOrder();
        order.Items = new List<ModelOrderItem>();

        // Act
        var result = _mapper.Map<ModelOrder, OrderDto>(order);

        // Assert
        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Map_Order_WithCurrentStatus_ShouldMapNestedStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        order.CurrentStatus = new OrderStatusInfo
        {
            Id = 1,
            Status = OrderStatus.Processing,
            Note = "Order is being prepared",
            ChangedBy = "System",
            ChangedAt = DateTime.UtcNow,
            IsTerminal = false
        };

        // Act
        var result = _mapper.Map<ModelOrder, OrderDto>(order);

        // Assert
        result.CurrentStatus.Should().NotBeNull();
        result.CurrentStatus.Status.Should().Be(OrderStatus.Processing);
        result.CurrentStatus.Note.Should().Be("Order is being prepared");
    }

    #endregion

    #region Enum Mapping Tests

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.CreditCard)]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.PayPal)]
    [InlineData(PaymentMethod.CryptoCurrency)]
    public void Map_Order_ShouldMapPaymentMethodEnum(PaymentMethod paymentMethod)
    {
        // Arrange
        var order = CreateTestOrder();
        order.PaymentMethod = paymentMethod;

        // Act
        var result = _mapper.Map<ModelOrder, OrderDto>(order);

        // Assert
        result.PaymentMethod.Should().Be(paymentMethod);
    }

    [Theory]
    [InlineData(Gender.Male)]
    [InlineData(Gender.Female)]
    [InlineData(Gender.Other)]
    [InlineData(Gender.Unspecified)]
    public void Map_Customer_ShouldMapNullableGenderEnum(Gender gender)
    {
        // Arrange
        var customer = CreateTestCustomer();
        customer.Gender = gender;

        // Act
        var result = _mapper.Map<Customer, CustomerDto>(customer);

        // Assert
        result.Gender.Should().Be(gender);
    }

    #endregion

    #region Collection Mapping Tests

    [Fact]
    public void MapList_Addresses_ShouldMapAllItems()
    {
        // Arrange
        var addresses = new List<Address>
        {
            new() { Id = 1, City = "New York", Country = "USA" },
            new() { Id = 2, City = "London", Country = "UK" },
            new() { Id = 3, City = "Tokyo", Country = "Japan" }
        };

        // Act
        var result = _mapper.MapList<Address, AddressDto>(addresses);

        // Assert
        result.Should().HaveCount(3);
        result.Select(a => a.City).Should().ContainInOrder("New York", "London", "Tokyo");
    }

    [Fact]
    public void MapArray_OrderItems_ShouldMapAllItems()
    {
        // Arrange
        var items = new ModelOrderItem[]
        {
            new() { Id = 1, ProductName = "Item1", Quantity = 1, UnitPrice = 10m },
            new() { Id = 2, ProductName = "Item2", Quantity = 2, UnitPrice = 20m }
        };

        // Act
        var result = _mapper.MapArray<ModelOrderItem, OrderItemDto>(items);

        // Assert
        result.Should().HaveCount(2);
        result[0].ProductName.Should().Be("Item1");
        result[1].ProductName.Should().Be("Item2");
    }

    #endregion

    #region Helper Methods

    private static Customer CreateTestCustomer()
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-0123",
            DateOfBirth = new DateOnly(1990, 5, 15),
            Gender = Gender.Male,
            LoyaltyPoints = 500,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            BillingAddress = new Address
            {
                Id = 1,
                Street = "100 Billing St",
                City = "Chicago",
                ZipCode = "60601",
                Country = "USA",
                IsDefault = true
            }
        };
    }

    private static ModelOrder CreateTestOrder()
    {
        return new ModelOrder
        {
            Id = 1,
            OrderNumber = "ORD-2024-001",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 299.99m,
            PaymentMethod = PaymentMethod.CreditCard,
            IsPaid = true,
            CurrentStatus = new OrderStatusInfo
            {
                Status = OrderStatus.Created,
                ChangedBy = "System",
                ChangedAt = DateTime.UtcNow
            },
            Items = new List<ModelOrderItem>()
        };
    }

    #endregion
}