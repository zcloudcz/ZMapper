using FluentAssertions;
using ZMapper.Tests.Model;
using Xunit;

// Type aliases to avoid namespace conflicts with ZMapper.Tests.Order (defined in ReverseMappingTests.cs)
using ModelOrder = ZMapper.Tests.Model.Order;
using ModelOrderItem = ZMapper.Tests.Model.OrderItem;

namespace ZMapper.Tests.ComplexConditional;

// === DTOs with properties suitable for conditional mapping ===

/// <summary>
/// Address DTO that includes a validity flag for conditional testing.
/// </summary>
public class ConditionalAddressDto
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

/// <summary>
/// Customer DTO for conditional mapping - includes nullable and enum properties.
/// </summary>
public class ConditionalCustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; }
    public ConditionalAddressDto BillingAddress { get; set; } = new();
    public ConditionalAddressDto? ShippingAddress { get; set; }
}

/// <summary>
/// Order item DTO for conditional mapping - price and discount conditions.
/// </summary>
public class ConditionalOrderItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public bool IsGift { get; set; }
}

/// <summary>
/// Order DTO for conditional mapping - combines nested objects, collections, enums.
/// </summary>
public class ConditionalOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public OrderStatusInfo CurrentStatus { get; set; } = new();
    public ICollection<ConditionalOrderItemDto> Items { get; set; } = new List<ConditionalOrderItemDto>();
}

// === Destination types for conditional mapping ===

/// <summary>
/// Destination with conditional properties. Defaults are used to verify
/// that properties are NOT overwritten when conditions fail.
/// </summary>
public class ConditionalCustomerTarget
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; } = -1m; // Sentinel default to detect unmapped
    public bool IsActive { get; set; }
    public ConditionalAddressDto BillingAddress { get; set; } = new();
    public ConditionalAddressDto? ShippingAddress { get; set; }
}

public class ConditionalOrderItemTarget
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; } = -1m; // Sentinel default to detect unmapped
    public bool IsGift { get; set; }
}

public class ConditionalOrderTarget
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; } = -1m; // Sentinel default
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; } = "UNMAPPED"; // Sentinel default
    public OrderStatusInfo CurrentStatus { get; set; } = new();
    public ICollection<ConditionalOrderItemTarget> Items { get; set; } = new List<ConditionalOrderItemTarget>();
}

// === Mapper Configuration ===

public partial class ComplexConditionalMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Address: map coordinates only when latitude is non-zero (valid GPS)
        config.CreateMap<ConditionalAddressDto, ConditionalAddressDto>();

        // Customer: conditional on multiple property types
        config.CreateMap<ConditionalCustomerDto, ConditionalCustomerTarget>()
            // Only map Phone when it's not null or empty
            .ForMember(dest => dest.Phone, opt =>
            {
                opt.MapFrom(src => src.Phone);
                opt.When(src => !string.IsNullOrEmpty(src.Phone));
            })
            // Only map CreditLimit when customer is active
            .ForMember(dest => dest.CreditLimit, opt =>
            {
                opt.MapFrom(src => src.CreditLimit);
                opt.When(src => src.IsActive);
            })
            // Only map Gender when it has a value (non-null enum)
            .ForMember(dest => dest.Gender, opt =>
            {
                opt.MapFrom(src => src.Gender);
                opt.When(src => src.Gender.HasValue);
            })
            // Only map LoyaltyPoints when positive
            .ForMember(dest => dest.LoyaltyPoints, opt =>
            {
                opt.MapFrom(src => src.LoyaltyPoints);
                opt.When(src => src.LoyaltyPoints > 0);
            });

        // OrderItem: conditional on price-related fields
        config.CreateMap<ConditionalOrderItemDto, ConditionalOrderItemTarget>()
            // Only map Discount when it has a value and is positive
            .ForMember(dest => dest.Discount, opt =>
            {
                opt.MapFrom(src => src.Discount);
                opt.When(src => src.Discount.HasValue && src.Discount.Value > 0);
            })
            // Only map ProductCode when not null
            .ForMember(dest => dest.ProductCode, opt =>
            {
                opt.MapFrom(src => src.ProductCode);
                opt.When(src => src.ProductCode != null);
            });

        // Order: conditional on shipping and tracking fields
        config.CreateMap<ConditionalOrderDto, ConditionalOrderTarget>()
            // Only map ShippingCost when order is paid
            .ForMember(dest => dest.ShippingCost, opt =>
            {
                opt.MapFrom(src => src.ShippingCost);
                opt.When(src => src.IsPaid);
            })
            // Only map TrackingNumber when it's not null/empty
            .ForMember(dest => dest.TrackingNumber, opt =>
            {
                opt.MapFrom(src => src.TrackingNumber);
                opt.When(src => !string.IsNullOrEmpty(src.TrackingNumber));
            });

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for conditional mapping (When()) with complex types:
/// nested objects, collections, enums, nullable types.
/// </summary>
public class ComplexConditionalMappingTests
{
    private readonly IMapper _mapper;

    public ComplexConditionalMappingTests()
    {
        _mapper = ComplexConditionalMapperConfig.CreateMapper();
    }

    // ==================== Customer conditional tests ====================

    [Fact]
    public void When_CustomerAllConditionsMet_ShouldMapAllProperties()
    {
        // Arrange - all conditions are satisfied
        var dto = new ConditionalCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Phone = "+420123456789",       // Not empty â†’ mapped
            Gender = Gender.Male,          // HasValue â†’ mapped
            LoyaltyPoints = 500,           // > 0 â†’ mapped
            CreditLimit = 10000m,
            IsActive = true,               // Active â†’ CreditLimit mapped
            BillingAddress = new ConditionalAddressDto { City = "Prague" }
        };

        // Act
        var result = _mapper.Map<ConditionalCustomerDto, ConditionalCustomerTarget>(dto);

        // Assert - all conditional properties should be mapped
        result.Phone.Should().Be("+420123456789");
        result.Gender.Should().Be(Gender.Male);
        result.LoyaltyPoints.Should().Be(500);
        result.CreditLimit.Should().Be(10000m);
    }

    [Fact]
    public void When_CustomerNoConditionsMet_ShouldKeepDefaults()
    {
        // Arrange - no conditions satisfied
        var dto = new ConditionalCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane@example.com",
            Phone = null,                  // Null â†’ NOT mapped
            Gender = null,                 // No value â†’ NOT mapped
            LoyaltyPoints = 0,             // Not > 0 â†’ NOT mapped
            CreditLimit = 5000m,
            IsActive = false,              // Inactive â†’ CreditLimit NOT mapped
        };

        // Act
        var result = _mapper.Map<ConditionalCustomerDto, ConditionalCustomerTarget>(dto);

        // Assert - conditional properties should retain defaults
        result.Phone.Should().BeNull();         // Default for string?
        result.Gender.Should().BeNull();        // Default for Gender?
        result.LoyaltyPoints.Should().Be(0);    // Default for int
        result.CreditLimit.Should().Be(-1m);    // Sentinel default (unmapped)

        // Non-conditional properties should still be mapped
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public void When_CustomerEmptyPhone_ShouldNotMapPhone()
    {
        // Arrange - empty string (not null, but still fails IsNullOrEmpty condition)
        var dto = new ConditionalCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Bob",
            LastName = "Wilson",
            Email = "bob@example.com",
            Phone = "",                    // Empty â†’ NOT mapped
            IsActive = true,
            LoyaltyPoints = 100,
            Gender = Gender.Other
        };

        // Act
        var result = _mapper.Map<ConditionalCustomerDto, ConditionalCustomerTarget>(dto);

        // Assert
        result.Phone.Should().BeNull();         // Not mapped, keeps default
        result.LoyaltyPoints.Should().Be(100);  // Condition met, mapped
        result.Gender.Should().Be(Gender.Other); // Condition met, mapped
    }

    [Fact]
    public void When_CustomerWithNullableEnum_NullValue_ShouldNotMapGender()
    {
        // Arrange
        var dto = new ConditionalCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Alice",
            LastName = "Johnson",
            Email = "alice@example.com",
            Gender = null,      // Nullable enum without value â†’ NOT mapped
            IsActive = true,
            LoyaltyPoints = 1
        };

        // Act
        var result = _mapper.Map<ConditionalCustomerDto, ConditionalCustomerTarget>(dto);

        // Assert
        result.Gender.Should().BeNull();
        result.FirstName.Should().Be("Alice");
    }

    [Fact]
    public void When_InactiveCustomer_ShouldNotMapCreditLimit()
    {
        // Arrange - CreditLimit condition depends on IsActive
        var dto = new ConditionalCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Inactive",
            LastName = "User",
            Email = "inactive@example.com",
            IsActive = false,              // Inactive â†’ CreditLimit stays at sentinel
            CreditLimit = 99999m,
            LoyaltyPoints = 1
        };

        // Act
        var result = _mapper.Map<ConditionalCustomerDto, ConditionalCustomerTarget>(dto);

        // Assert - CreditLimit should NOT be mapped because IsActive is false
        result.CreditLimit.Should().Be(-1m); // Sentinel default preserved
        result.IsActive.Should().BeFalse();
    }

    // ==================== OrderItem conditional tests ====================

    [Fact]
    public void When_OrderItemWithDiscount_ShouldMapDiscount()
    {
        // Arrange
        var dto = new ConditionalOrderItemDto
        {
            Id = 1,
            ProductName = "Widget",
            ProductCode = "WDG-001",
            Quantity = 5,
            UnitPrice = 25.00m,
            Discount = 10.00m,  // HasValue && > 0 â†’ mapped
            IsGift = false
        };

        // Act
        var result = _mapper.Map<ConditionalOrderItemDto, ConditionalOrderItemTarget>(dto);

        // Assert
        result.Discount.Should().Be(10.00m);
        result.ProductCode.Should().Be("WDG-001");
        result.ProductName.Should().Be("Widget");
    }

    [Fact]
    public void When_OrderItemWithNullDiscount_ShouldNotMapDiscount()
    {
        // Arrange
        var dto = new ConditionalOrderItemDto
        {
            Id = 2,
            ProductName = "Gadget",
            ProductCode = "GDG-002",
            Quantity = 1,
            UnitPrice = 100.00m,
            Discount = null,    // No value â†’ NOT mapped
            IsGift = true
        };

        // Act
        var result = _mapper.Map<ConditionalOrderItemDto, ConditionalOrderItemTarget>(dto);

        // Assert
        result.Discount.Should().Be(-1m);   // Sentinel default preserved
        result.IsGift.Should().BeTrue();
    }

    [Fact]
    public void When_OrderItemWithZeroDiscount_ShouldNotMapDiscount()
    {
        // Arrange - Discount has value but is 0 (fails > 0 condition)
        var dto = new ConditionalOrderItemDto
        {
            Id = 3,
            ProductName = "NoDiscount",
            Quantity = 2,
            UnitPrice = 50.00m,
            Discount = 0m,       // HasValue but NOT > 0 â†’ NOT mapped
        };

        // Act
        var result = _mapper.Map<ConditionalOrderItemDto, ConditionalOrderItemTarget>(dto);

        // Assert
        result.Discount.Should().Be(-1m); // Sentinel default, condition failed
    }

    [Fact]
    public void When_OrderItemWithNullProductCode_ShouldNotMapProductCode()
    {
        // Arrange
        var dto = new ConditionalOrderItemDto
        {
            Id = 4,
            ProductName = "Unnamed Product",
            ProductCode = null,    // Null â†’ NOT mapped
            Quantity = 1,
            UnitPrice = 10.00m,
            Discount = 5.00m
        };

        // Act
        var result = _mapper.Map<ConditionalOrderItemDto, ConditionalOrderItemTarget>(dto);

        // Assert
        result.ProductCode.Should().BeNull();  // Default null, not mapped
        result.Discount.Should().Be(5.00m);    // Condition met, mapped
    }

    // ==================== Order conditional tests ====================

    [Fact]
    public void When_PaidOrderWithTracking_ShouldMapShippingCostAndTracking()
    {
        // Arrange
        var dto = new ConditionalOrderDto
        {
            Id = 100,
            OrderNumber = "ORD-100",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 500.00m,
            ShippingCost = 15.99m,
            PaymentMethod = PaymentMethod.CreditCard,
            IsPaid = true,                         // Paid â†’ ShippingCost mapped
            TrackingNumber = "TRACK-123-ABC",      // Not empty â†’ mapped
        };

        // Act
        var result = _mapper.Map<ConditionalOrderDto, ConditionalOrderTarget>(dto);

        // Assert
        result.ShippingCost.Should().Be(15.99m);
        result.TrackingNumber.Should().Be("TRACK-123-ABC");
        result.OrderNumber.Should().Be("ORD-100");
        result.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
    }

    [Fact]
    public void When_UnpaidOrder_ShouldNotMapShippingCost()
    {
        // Arrange
        var dto = new ConditionalOrderDto
        {
            Id = 101,
            OrderNumber = "ORD-101",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 200.00m,
            ShippingCost = 9.99m,
            IsPaid = false,                        // Unpaid â†’ ShippingCost NOT mapped
            TrackingNumber = "TRACK-456",
        };

        // Act
        var result = _mapper.Map<ConditionalOrderDto, ConditionalOrderTarget>(dto);

        // Assert
        result.ShippingCost.Should().Be(-1m);          // Sentinel default preserved
        result.TrackingNumber.Should().Be("TRACK-456"); // Condition met (not empty)
        result.IsPaid.Should().BeFalse();
    }

    [Fact]
    public void When_OrderWithNoTracking_ShouldNotMapTrackingNumber()
    {
        // Arrange
        var dto = new ConditionalOrderDto
        {
            Id = 102,
            OrderNumber = "ORD-102",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 100.00m,
            IsPaid = true,
            TrackingNumber = null,                  // Null â†’ NOT mapped
        };

        // Act
        var result = _mapper.Map<ConditionalOrderDto, ConditionalOrderTarget>(dto);

        // Assert
        result.TrackingNumber.Should().Be("UNMAPPED"); // Sentinel default preserved
        result.ShippingCost.Should().BeNull();          // Paid but ShippingCost is null in source
    }

    [Fact]
    public void When_OrderWithEmptyTracking_ShouldNotMapTrackingNumber()
    {
        // Arrange - empty string (not null, but fails IsNullOrEmpty check)
        var dto = new ConditionalOrderDto
        {
            Id = 103,
            OrderNumber = "ORD-103",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 75.00m,
            IsPaid = true,
            TrackingNumber = "",                    // Empty â†’ NOT mapped
        };

        // Act
        var result = _mapper.Map<ConditionalOrderDto, ConditionalOrderTarget>(dto);

        // Assert
        result.TrackingNumber.Should().Be("UNMAPPED"); // Sentinel default preserved
    }

    [Fact]
    public void When_OrderWithAllConditionsFailing_ShouldMapOnlyUnconditionalProperties()
    {
        // Arrange - both conditions fail
        var dto = new ConditionalOrderDto
        {
            Id = 104,
            OrderNumber = "ORD-104",
            OrderedAt = new DateTime(2025, 6, 15),
            TotalAmount = 999.99m,
            ShippingCost = 25.00m,
            PaymentMethod = PaymentMethod.PayPal,
            IsPaid = false,                        // Unpaid â†’ ShippingCost NOT mapped
            TrackingNumber = null,                 // Null â†’ NOT mapped
        };

        // Act
        var result = _mapper.Map<ConditionalOrderDto, ConditionalOrderTarget>(dto);

        // Assert - conditional properties keep sentinel defaults
        result.ShippingCost.Should().Be(-1m);
        result.TrackingNumber.Should().Be("UNMAPPED");

        // Non-conditional properties should be mapped normally
        result.Id.Should().Be(104);
        result.OrderNumber.Should().Be("ORD-104");
        result.TotalAmount.Should().Be(999.99m);
        result.PaymentMethod.Should().Be(PaymentMethod.PayPal);
    }

    [Fact]
    public void When_OrderWithNegativeDiscount_InItem_ShouldNotMapDiscount()
    {
        // Arrange - negative discount value (fails > 0 condition)
        var dto = new ConditionalOrderItemDto
        {
            Id = 5,
            ProductName = "Surcharge Item",
            Quantity = 1,
            UnitPrice = 100.00m,
            Discount = -5.00m,     // Negative â†’ fails > 0 condition
        };

        // Act
        var result = _mapper.Map<ConditionalOrderItemDto, ConditionalOrderItemTarget>(dto);

        // Assert
        result.Discount.Should().Be(-1m); // Sentinel default preserved
    }

    // ==================== Batch conditional tests ====================

    [Fact]
    public void When_BatchConditionalMapping_ShouldApplyConditionsPerItem()
    {
        // Arrange - mix of items: some with discount, some without
        var items = new[]
        {
            new ConditionalOrderItemDto { Id = 1, ProductName = "A", Quantity = 1, UnitPrice = 10m, Discount = 5m, ProductCode = "A-1" },
            new ConditionalOrderItemDto { Id = 2, ProductName = "B", Quantity = 2, UnitPrice = 20m, Discount = null, ProductCode = null },
            new ConditionalOrderItemDto { Id = 3, ProductName = "C", Quantity = 3, UnitPrice = 30m, Discount = 0m, ProductCode = "C-3" },
        };

        // Act
        var results = _mapper.MapArray<ConditionalOrderItemDto, ConditionalOrderItemTarget>(items);

        // Assert
        results.Should().HaveCount(3);

        // Item 1: discount > 0 and ProductCode not null â†’ both mapped
        results[0].Discount.Should().Be(5m);
        results[0].ProductCode.Should().Be("A-1");

        // Item 2: discount null and ProductCode null â†’ neither mapped
        results[1].Discount.Should().Be(-1m);
        results[1].ProductCode.Should().BeNull();

        // Item 3: discount = 0 (fails > 0) but ProductCode not null â†’ only code mapped
        results[2].Discount.Should().Be(-1m);
        results[2].ProductCode.Should().Be("C-3");
    }

    [Fact]
    public void When_ListConditionalMapping_ShouldApplyConditionsPerItem()
    {
        // Arrange
        var customers = new List<ConditionalCustomerDto>
        {
            new() { Id = Guid.NewGuid(), FirstName = "Active", LastName = "User", Email = "a@b.com",
                     IsActive = true, CreditLimit = 1000m, LoyaltyPoints = 100, Phone = "123" },
            new() { Id = Guid.NewGuid(), FirstName = "Inactive", LastName = "User", Email = "c@d.com",
                     IsActive = false, CreditLimit = 2000m, LoyaltyPoints = 0, Phone = null },
        };

        // Act
        var results = _mapper.MapList<ConditionalCustomerDto, ConditionalCustomerTarget>(customers);

        // Assert
        results.Should().HaveCount(2);

        // Active user: CreditLimit mapped, LoyaltyPoints > 0 mapped, Phone not empty mapped
        results[0].CreditLimit.Should().Be(1000m);
        results[0].LoyaltyPoints.Should().Be(100);
        results[0].Phone.Should().Be("123");

        // Inactive user: CreditLimit NOT mapped, LoyaltyPoints = 0 NOT mapped, Phone null NOT mapped
        results[1].CreditLimit.Should().Be(-1m);
        results[1].LoyaltyPoints.Should().Be(0);
        results[1].Phone.Should().BeNull();
    }
}
