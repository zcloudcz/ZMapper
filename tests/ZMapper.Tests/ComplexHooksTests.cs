using System.Globalization;
using FluentAssertions;
using ZMapper.Tests.Model;
using Xunit;

// Type aliases to avoid namespace conflicts with ZMapper.Tests.Order (defined in ReverseMappingTests.cs)
using ModelOrder = ZMapper.Tests.Model.Order;
using ModelOrderItem = ZMapper.Tests.Model.OrderItem;

namespace ZMapper.Tests.ComplexHooks;

// === DTOs for hook testing ===

public class HookAddressDto
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public bool IsDefault { get; set; }
}

public class HookCustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; }
    public HookAddressDto BillingAddress { get; set; } = new();
    public HookAddressDto? ShippingAddress { get; set; }
}

public class HookOrderItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public bool IsGift { get; set; }
}

public class HookOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public ICollection<HookOrderItemDto> Items { get; set; } = new List<HookOrderItemDto>();
}

// === Destination types for hook testing ===

public class HookAddressTarget
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public bool IsDefault { get; set; }
    // Set by AfterMap hook
    public string FullAddress { get; set; } = string.Empty;
}

public class HookCustomerTarget
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public bool IsActive { get; set; }
    public HookAddressTarget BillingAddress { get; set; } = new();
    public HookAddressTarget? ShippingAddress { get; set; }
    // Set by hooks
    public string DisplayName { get; set; } = string.Empty;
    public DateTime MappedAt { get; set; }
    public string LoyaltyTier { get; set; } = string.Empty;
}

public class HookOrderItemTarget
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public bool IsGift { get; set; }
    // Computed by AfterMap
    public decimal LineTotal { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
}

public class HookOrderTarget
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public ICollection<HookOrderItemTarget> Items { get; set; } = new List<HookOrderItemTarget>();
    // Set by hooks
    public string OrderSummary { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
}

// === Mapper Configurations (each test group gets its own config) ===

/// <summary>
/// Config with AfterMap on Address: computes FullAddress from mapped properties.
/// The source generator requires a separate ConfigureMapper method to detect hooks.
/// </summary>
public partial class HookAddressMapperConfig
{
    public static void ConfigureMapper(MapperConfiguration config)
    {
        config.CreateMap<HookAddressDto, HookAddressTarget>()
            .IgnoreNonExisting() // FullAddress is set by AfterMap hook, not from source
            .AfterMap((src, dest) =>
            {
                // Compute FullAddress from already-mapped properties
                dest.FullAddress = $"{dest.Street}, {dest.City}, {dest.ZipCode}, {dest.Country}";
            });
    }

    public static IMapper CreateMapper()
    {
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Config with both BeforeMap and AfterMap on Customer.
/// BeforeMap: sets MappedAt timestamp.
/// AfterMap: computes DisplayName and LoyaltyTier from mapped data.
/// </summary>
public partial class HookCustomerMapperConfig
{
    private static int _beforeMapCount;
    private static int _afterMapCount;
    private static readonly List<string> _hookLog = new();

    public static int BeforeMapCount => _beforeMapCount;
    public static int AfterMapCount => _afterMapCount;
    public static IReadOnlyList<string> HookLog => _hookLog;

    public static void ResetCounters()
    {
        _beforeMapCount = 0;
        _afterMapCount = 0;
        _hookLog.Clear();
    }

    // Bug 1 fix: Multiple mappings with hooks now work in same config
    // (source generator uses unique variable name suffixes).
    public static void ConfigureMapper(MapperConfiguration config)
    {
        config.CreateMap<HookAddressDto, HookAddressTarget>()
            .IgnoreNonExisting() // FullAddress is set by AfterMap hook
            .AfterMap((src, dest) =>
            {
                // Compute FullAddress from already-mapped properties
                dest.FullAddress = $"{dest.Street}, {dest.City}, {dest.ZipCode}, {dest.Country}";
            });

        config.CreateMap<HookCustomerDto, HookCustomerTarget>()
            .IgnoreNonExisting() // DisplayName, MappedAt, LoyaltyTier are set by hooks
            .BeforeMap((src, dest) =>
            {
                _beforeMapCount++;
                _hookLog.Add($"BeforeMap: {src.FirstName} {src.LastName}");
                dest.MappedAt = DateTime.UtcNow;
            })
            .AfterMap((src, dest) =>
            {
                _afterMapCount++;
                _hookLog.Add($"AfterMap: {dest.FirstName} {dest.LastName}");

                // Compute DisplayName from already-mapped first/last name
                dest.DisplayName = $"{dest.LastName}, {dest.FirstName}";

                // Determine loyalty tier based on mapped LoyaltyPoints
                dest.LoyaltyTier = dest.LoyaltyPoints switch
                {
                    >= 1000 => "Gold",
                    >= 500 => "Silver",
                    >= 100 => "Bronze",
                    _ => "Standard"
                };
            });
    }

    public static IMapper CreateMapper()
    {
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Config with AfterMap on OrderItem: computes LineTotal and FormattedPrice.
/// </summary>
public partial class HookOrderItemMapperConfig
{
    public static void ConfigureMapper(MapperConfiguration config)
    {
        config.CreateMap<HookOrderItemDto, HookOrderItemTarget>()
            .IgnoreNonExisting() // LineTotal, FormattedPrice are set by AfterMap hook
            .AfterMap((src, dest) =>
            {
                // Compute line total from mapped properties
                dest.LineTotal = dest.Quantity * dest.UnitPrice - (dest.Discount ?? 0m);
                dest.FormattedPrice = $"${dest.LineTotal.ToString("F2", CultureInfo.InvariantCulture)}";
            });
    }

    public static IMapper CreateMapper()
    {
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Config with BeforeMap and AfterMap on Order (with nested items).
/// BeforeMap: sets ProcessedAt and ProcessedBy.
/// AfterMap: computes GrandTotal and OrderSummary from mapped data.
/// </summary>
public partial class HookOrderMapperConfig
{
    private static int _orderHookCount;
    public static int OrderHookCount => _orderHookCount;

    public static void ResetCounters()
    {
        _orderHookCount = 0;
    }

    // Bug 1 fix: Multiple mappings with hooks now work in same config.
    public static void ConfigureMapper(MapperConfiguration config)
    {
        config.CreateMap<HookOrderItemDto, HookOrderItemTarget>()
            .IgnoreNonExisting() // LineTotal, FormattedPrice are set by AfterMap hook
            .AfterMap((src, dest) =>
            {
                // Compute line total from mapped properties
                dest.LineTotal = dest.Quantity * dest.UnitPrice - (dest.Discount ?? 0m);
                dest.FormattedPrice = $"${dest.LineTotal.ToString("F2", CultureInfo.InvariantCulture)}";
            });

        config.CreateMap<HookOrderDto, HookOrderTarget>()
            .IgnoreNonExisting() // OrderSummary, ProcessedAt, ProcessedBy, GrandTotal are set by hooks
            .BeforeMap((src, dest) =>
            {
                _orderHookCount++;
                dest.ProcessedAt = DateTime.UtcNow;
                dest.ProcessedBy = "ZMapper-HookTest";
            })
            .AfterMap((src, dest) =>
            {
                // Compute grand total including shipping
                dest.GrandTotal = dest.TotalAmount + (dest.ShippingCost ?? 0m);

                // Build order summary from mapped data
                var itemCount = dest.Items?.Count ?? 0;
                dest.OrderSummary = $"Order {dest.OrderNumber}: {itemCount} item(s), " +
                                    $"Total: ${dest.GrandTotal.ToString("F2", CultureInfo.InvariantCulture)}, " +
                                    $"Paid: {(dest.IsPaid ? "Yes" : "No")}";
            });
    }

    public static IMapper CreateMapper()
    {
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for BeforeMap/AfterMap hooks with complex types:
/// nested objects, collections, computed properties, batch mapping.
/// </summary>
public class ComplexHooksTests
{
    // ==================== Address AfterMap tests ====================

    [Fact]
    public void AfterMap_Address_ShouldComputeFullAddress()
    {
        // Arrange
        var mapper = HookAddressMapperConfig.CreateMapper();
        var dto = new HookAddressDto
        {
            Id = 1,
            Street = "123 Main St",
            City = "Prague",
            ZipCode = "11000",
            Country = "Czech Republic",
            State = null,
            IsDefault = true
        };

        // Act
        var result = mapper.Map<HookAddressDto, HookAddressTarget>(dto);

        // Assert - mapped properties
        result.Street.Should().Be("123 Main St");
        result.City.Should().Be("Prague");
        result.ZipCode.Should().Be("11000");
        result.Country.Should().Be("Czech Republic");

        // AfterMap computed property
        result.FullAddress.Should().Be("123 Main St, Prague, 11000, Czech Republic");
    }

    // ==================== Customer BeforeMap + AfterMap tests ====================

    [Fact]
    public void Hooks_Customer_ShouldSetTimestampAndComputeDisplayName()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var dto = new HookCustomerDto
        {
            Id = Guid.NewGuid(),
            FirstName = "Jan",
            LastName = "Novak",
            Email = "jan@novak.cz",
            LoyaltyPoints = 750,
            IsActive = true,
            BillingAddress = new HookAddressDto
            {
                Street = "Vodickova 10",
                City = "Prague"
            }
        };

        // Act
        var before = DateTime.UtcNow;
        var result = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto);

        // Assert - BeforeMap set MappedAt
        result.MappedAt.Should().BeOnOrAfter(before);
        result.MappedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // AfterMap computed DisplayName from mapped properties
        result.DisplayName.Should().Be("Novak, Jan");

        // AfterMap computed LoyaltyTier (750 points â†’ Silver)
        result.LoyaltyTier.Should().Be("Silver");

        // Standard mapping still works
        result.FirstName.Should().Be("Jan");
        result.LastName.Should().Be("Novak");
        result.Email.Should().Be("jan@novak.cz");
        result.LoyaltyPoints.Should().Be(750);
    }

    [Fact]
    public void Hooks_Customer_ShouldTrackCallCount()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();

        var dto1 = new HookCustomerDto { FirstName = "Alice", LastName = "A", Email = "a@a.com", LoyaltyPoints = 50 };
        var dto2 = new HookCustomerDto { FirstName = "Bob", LastName = "B", Email = "b@b.com", LoyaltyPoints = 200 };
        var dto3 = new HookCustomerDto { FirstName = "Charlie", LastName = "C", Email = "c@c.com", LoyaltyPoints = 1500 };

        // Act
        var r1 = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto1);
        var r2 = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto2);
        var r3 = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto3);

        // Assert - hooks called for each mapping
        HookCustomerMapperConfig.BeforeMapCount.Should().Be(3);
        HookCustomerMapperConfig.AfterMapCount.Should().Be(3);

        // Each customer gets correct loyalty tier
        r1.LoyaltyTier.Should().Be("Standard");   // 50 points
        r2.LoyaltyTier.Should().Be("Bronze");      // 200 points
        r3.LoyaltyTier.Should().Be("Gold");        // 1500 points
    }

    [Fact]
    public void Hooks_Customer_ShouldLogInCorrectOrder()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var dto = new HookCustomerDto { FirstName = "Eva", LastName = "Svobodova", Email = "e@s.com" };

        // Act
        mapper.Map<HookCustomerDto, HookCustomerTarget>(dto);

        // Assert - BeforeMap runs before AfterMap
        HookCustomerMapperConfig.HookLog.Should().HaveCount(2);
        HookCustomerMapperConfig.HookLog[0].Should().Be("BeforeMap: Eva Svobodova");
        HookCustomerMapperConfig.HookLog[1].Should().Be("AfterMap: Eva Svobodova");
    }

    [Fact]
    public void Hooks_Customer_LoyaltyTierBoundaries_ShouldBeCorrect()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();

        // Act & Assert - test boundary values for loyalty tiers
        var standard = mapper.Map<HookCustomerDto, HookCustomerTarget>(
            new HookCustomerDto { FirstName = "S", LastName = "S", Email = "s@s", LoyaltyPoints = 99 });
        standard.LoyaltyTier.Should().Be("Standard");

        var bronze = mapper.Map<HookCustomerDto, HookCustomerTarget>(
            new HookCustomerDto { FirstName = "B", LastName = "B", Email = "b@b", LoyaltyPoints = 100 });
        bronze.LoyaltyTier.Should().Be("Bronze");

        var silver = mapper.Map<HookCustomerDto, HookCustomerTarget>(
            new HookCustomerDto { FirstName = "Si", LastName = "Si", Email = "si@si", LoyaltyPoints = 500 });
        silver.LoyaltyTier.Should().Be("Silver");

        var gold = mapper.Map<HookCustomerDto, HookCustomerTarget>(
            new HookCustomerDto { FirstName = "G", LastName = "G", Email = "g@g", LoyaltyPoints = 1000 });
        gold.LoyaltyTier.Should().Be("Gold");
    }

    [Fact]
    public void Hooks_CustomerWithNestedAddress_ShouldMapNestedCorrectly()
    {
        // Arrange - Customer hooks run; Address is mapped without hooks (convention-based)
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var dto = new HookCustomerDto
        {
            FirstName = "Petr",
            LastName = "Dvorak",
            Email = "petr@dvorak.cz",
            LoyaltyPoints = 600,
            BillingAddress = new HookAddressDto
            {
                Street = "Narodni 5",
                City = "Brno",
                ZipCode = "60200",
                Country = "CZ"
            },
            ShippingAddress = new HookAddressDto
            {
                Street = "Husova 12",
                City = "Olomouc",
                ZipCode = "77900",
                Country = "CZ"
            }
        };

        // Act
        var result = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto);

        // Assert - Customer hooks ran
        result.DisplayName.Should().Be("Dvorak, Petr");
        result.LoyaltyTier.Should().Be("Silver");

        // Nested addresses mapped correctly (no hooks on Address in this config)
        result.BillingAddress.Street.Should().Be("Narodni 5");
        result.BillingAddress.City.Should().Be("Brno");
        result.ShippingAddress.Should().NotBeNull();
        result.ShippingAddress!.Street.Should().Be("Husova 12");
        result.ShippingAddress!.City.Should().Be("Olomouc");
    }

    [Fact]
    public void Hooks_CustomerWithNullShippingAddress_ShouldHandleGracefully()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var dto = new HookCustomerDto
        {
            FirstName = "Marie",
            LastName = "Kralova",
            Email = "marie@k.cz",
            LoyaltyPoints = 1200,
            BillingAddress = new HookAddressDto { Street = "A", City = "B" },
            ShippingAddress = null   // No shipping address
        };

        // Act
        var result = mapper.Map<HookCustomerDto, HookCustomerTarget>(dto);

        // Assert
        result.DisplayName.Should().Be("Kralova, Marie");
        result.LoyaltyTier.Should().Be("Gold");
        result.BillingAddress.Street.Should().Be("A");
        result.BillingAddress.City.Should().Be("B");
        result.ShippingAddress.Should().BeNull(); // Null handled safely
    }

    // ==================== OrderItem AfterMap tests ====================

    [Fact]
    public void AfterMap_OrderItem_ShouldComputeLineTotalAndFormattedPrice()
    {
        // Arrange
        var mapper = HookOrderItemMapperConfig.CreateMapper();
        var dto = new HookOrderItemDto
        {
            Id = 1,
            ProductName = "Widget",
            Quantity = 3,
            UnitPrice = 25.00m,
            Discount = 5.00m,
            IsGift = false
        };

        // Act
        var result = mapper.Map<HookOrderItemDto, HookOrderItemTarget>(dto);

        // Assert
        result.LineTotal.Should().Be(70.00m);  // 3 * 25 - 5 = 70
        result.FormattedPrice.Should().Be("$70.00");
    }

    [Fact]
    public void AfterMap_OrderItem_WithNoDiscount_ShouldComputeCorrectly()
    {
        // Arrange
        var mapper = HookOrderItemMapperConfig.CreateMapper();
        var dto = new HookOrderItemDto
        {
            Id = 2,
            ProductName = "Gadget",
            Quantity = 2,
            UnitPrice = 49.99m,
            Discount = null,   // No discount
            IsGift = true
        };

        // Act
        var result = mapper.Map<HookOrderItemDto, HookOrderItemTarget>(dto);

        // Assert
        result.LineTotal.Should().Be(99.98m);  // 2 * 49.99 - 0 = 99.98
        result.FormattedPrice.Should().Be("$99.98");
    }

    // ==================== Order BeforeMap + AfterMap tests ====================

    [Fact]
    public void Hooks_Order_ShouldSetProcessedInfoAndComputeSummary()
    {
        // Arrange
        HookOrderMapperConfig.ResetCounters();
        var mapper = HookOrderMapperConfig.CreateMapper();
        var dto = new HookOrderDto
        {
            Id = 100,
            OrderNumber = "ORD-2025-100",
            OrderedAt = new DateTime(2025, 6, 15),
            TotalAmount = 250.00m,
            ShippingCost = 15.00m,
            PaymentMethod = PaymentMethod.CreditCard,
            IsPaid = true,
            Items = new List<HookOrderItemDto>
            {
                new() { Id = 1, ProductName = "Item A", Quantity = 2, UnitPrice = 50.00m, Discount = 5.00m },
                new() { Id = 2, ProductName = "Item B", Quantity = 1, UnitPrice = 100.00m },
                new() { Id = 3, ProductName = "Item C", Quantity = 3, UnitPrice = 15.00m, Discount = 2.50m }
            }
        };

        // Act
        var before = DateTime.UtcNow;
        var result = mapper.Map<HookOrderDto, HookOrderTarget>(dto);

        // Assert - BeforeMap
        result.ProcessedAt.Should().BeOnOrAfter(before);
        result.ProcessedBy.Should().Be("ZMapper-HookTest");

        // AfterMap computed GrandTotal = TotalAmount + ShippingCost
        result.GrandTotal.Should().Be(265.00m);  // 250 + 15

        // AfterMap computed OrderSummary
        result.OrderSummary.Should().Contain("ORD-2025-100");
        result.OrderSummary.Should().Contain("3 item(s)");
        result.OrderSummary.Should().Contain("$265.00");
        result.OrderSummary.Should().Contain("Paid: Yes");

        // Items were mapped (without hooks in this config - OrderItem hooks tested separately)
        result.Items.Should().HaveCount(3);
        var items = result.Items.ToList();
        items[0].ProductName.Should().Be("Item A");
        items[0].Quantity.Should().Be(2);
        items[0].UnitPrice.Should().Be(50.00m);
        items[1].ProductName.Should().Be("Item B");
        items[2].ProductName.Should().Be("Item C");
    }

    [Fact]
    public void Hooks_Order_UnpaidWithNoShipping_ShouldComputeCorrectly()
    {
        // Arrange
        HookOrderMapperConfig.ResetCounters();
        var mapper = HookOrderMapperConfig.CreateMapper();
        var dto = new HookOrderDto
        {
            Id = 101,
            OrderNumber = "ORD-2025-101",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 50.00m,
            ShippingCost = null,   // No shipping cost
            IsPaid = false,
            Items = new List<HookOrderItemDto>
            {
                new() { Id = 10, ProductName = "Single Item", Quantity = 1, UnitPrice = 50.00m }
            }
        };

        // Act
        var result = mapper.Map<HookOrderDto, HookOrderTarget>(dto);

        // Assert
        result.GrandTotal.Should().Be(50.00m);  // 50 + 0 (null shipping)
        result.OrderSummary.Should().Contain("1 item(s)");
        result.OrderSummary.Should().Contain("Paid: No");
    }

    [Fact]
    public void Hooks_Order_EmptyItems_ShouldHandleGracefully()
    {
        // Arrange
        HookOrderMapperConfig.ResetCounters();
        var mapper = HookOrderMapperConfig.CreateMapper();
        var dto = new HookOrderDto
        {
            Id = 102,
            OrderNumber = "ORD-EMPTY",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 0m,
            IsPaid = false,
            Items = new List<HookOrderItemDto>()  // Empty collection
        };

        // Act
        var result = mapper.Map<HookOrderDto, HookOrderTarget>(dto);

        // Assert
        result.Items.Should().BeEmpty();
        result.OrderSummary.Should().Contain("0 item(s)");
        result.GrandTotal.Should().Be(0m);
    }

    [Fact]
    public void Hooks_OrderHookCount_ShouldTrackMultipleMappings()
    {
        // Arrange
        HookOrderMapperConfig.ResetCounters();
        var mapper = HookOrderMapperConfig.CreateMapper();

        // Act - map 3 orders
        for (int i = 0; i < 3; i++)
        {
            mapper.Map<HookOrderDto, HookOrderTarget>(new HookOrderDto
            {
                Id = i,
                OrderNumber = $"ORD-{i}",
                OrderedAt = DateTime.UtcNow,
                Items = new List<HookOrderItemDto>()
            });
        }

        // Assert
        HookOrderMapperConfig.OrderHookCount.Should().Be(3);
    }

    // ==================== Batch hooks tests ====================

    // Bug 3 fix: MapArray and MapList now call Map_X_To_Y() instance method when hooks
    // are present, so hooks fire in batch mode too.

    [Fact]
    public void Batch_ArrayMapping_ShouldFireHooks()
    {
        // Arrange
        var mapper = HookOrderItemMapperConfig.CreateMapper();
        var items = new[]
        {
            new HookOrderItemDto { Id = 1, ProductName = "A", Quantity = 1, UnitPrice = 10m },
            new HookOrderItemDto { Id = 2, ProductName = "B", Quantity = 2, UnitPrice = 20m, Discount = 5m },
            new HookOrderItemDto { Id = 3, ProductName = "C", Quantity = 5, UnitPrice = 8m, Discount = 3m },
        };

        // Act
        var results = mapper.MapArray<HookOrderItemDto, HookOrderItemTarget>(items);

        // Assert - data is mapped correctly
        results.Should().HaveCount(3);
        results[0].ProductName.Should().Be("A");
        results[0].Quantity.Should().Be(1);
        results[0].UnitPrice.Should().Be(10m);
        results[1].ProductName.Should().Be("B");
        results[1].Discount.Should().Be(5m);
        results[2].ProductName.Should().Be("C");

        // Hooks now fire in batch mode (Bug 3 fix)
        results[0].LineTotal.Should().Be(10m);    // 1 * 10 - 0
        results[0].FormattedPrice.Should().Be("$10.00");
        results[1].LineTotal.Should().Be(35m);    // 2 * 20 - 5
        results[1].FormattedPrice.Should().Be("$35.00");
        results[2].LineTotal.Should().Be(37m);    // 5 * 8 - 3
        results[2].FormattedPrice.Should().Be("$37.00");
    }

    [Fact]
    public void Batch_ListMapping_ShouldFireHooks()
    {
        // Arrange
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var customers = new List<HookCustomerDto>
        {
            new() { FirstName = "A", LastName = "Alpha", Email = "a@a", LoyaltyPoints = 50 },
            new() { FirstName = "B", LastName = "Bravo", Email = "b@b", LoyaltyPoints = 150 },
        };

        // Act
        var results = mapper.MapList<HookCustomerDto, HookCustomerTarget>(customers);

        // Assert - data is mapped
        results.Should().HaveCount(2);
        results[0].FirstName.Should().Be("A");
        results[0].LastName.Should().Be("Alpha");
        results[1].FirstName.Should().Be("B");
        results[1].LastName.Should().Be("Bravo");

        // Hooks now fire in batch mode (Bug 3 fix)
        HookCustomerMapperConfig.BeforeMapCount.Should().Be(2);
        HookCustomerMapperConfig.AfterMapCount.Should().Be(2);
        results[0].DisplayName.Should().Be("Alpha, A");
        results[0].LoyaltyTier.Should().Be("Standard");
        results[1].DisplayName.Should().Be("Bravo, B");
        results[1].LoyaltyTier.Should().Be("Bronze");
    }

    [Fact]
    public void Batch_SingleMapLoop_ShouldRunHooksForEachItem()
    {
        // Arrange - use single Map<>() in a loop to get hooks
        HookCustomerMapperConfig.ResetCounters();
        var mapper = HookCustomerMapperConfig.CreateMapper();
        var customers = new List<HookCustomerDto>
        {
            new() { FirstName = "A", LastName = "Alpha", Email = "a@a", LoyaltyPoints = 50 },
            new() { FirstName = "B", LastName = "Bravo", Email = "b@b", LoyaltyPoints = 150 },
            new() { FirstName = "C", LastName = "Charlie", Email = "c@c", LoyaltyPoints = 550 },
            new() { FirstName = "D", LastName = "Delta", Email = "d@d", LoyaltyPoints = 1100 },
        };

        // Act - use single Map<>() which invokes hooks
        var results = customers
            .Select(c => mapper.Map<HookCustomerDto, HookCustomerTarget>(c))
            .ToList();

        // Assert - hooks ran for all 4 items
        HookCustomerMapperConfig.BeforeMapCount.Should().Be(4);
        HookCustomerMapperConfig.AfterMapCount.Should().Be(4);

        results[0].LoyaltyTier.Should().Be("Standard");
        results[0].DisplayName.Should().Be("Alpha, A");
        results[1].LoyaltyTier.Should().Be("Bronze");
        results[2].LoyaltyTier.Should().Be("Silver");
        results[3].LoyaltyTier.Should().Be("Gold");
    }

    [Fact]
    public void Batch_OrderArrayMapping_ShouldFireHooks()
    {
        // Arrange
        HookOrderMapperConfig.ResetCounters();
        var mapper = HookOrderMapperConfig.CreateMapper();
        var orders = new[]
        {
            new HookOrderDto
            {
                Id = 1, OrderNumber = "ORD-1", OrderedAt = DateTime.UtcNow,
                TotalAmount = 100m, ShippingCost = 10m, IsPaid = true,
                Items = new List<HookOrderItemDto>
                {
                    new() { Id = 1, ProductName = "X", Quantity = 2, UnitPrice = 50m }
                }
            },
            new HookOrderDto
            {
                Id = 2, OrderNumber = "ORD-2", OrderedAt = DateTime.UtcNow,
                TotalAmount = 200m, ShippingCost = null, IsPaid = false,
                Items = new List<HookOrderItemDto>
                {
                    new() { Id = 2, ProductName = "Y", Quantity = 1, UnitPrice = 100m },
                    new() { Id = 3, ProductName = "Z", Quantity = 3, UnitPrice = 33.33m }
                }
            }
        };

        // Act
        var results = mapper.MapArray<HookOrderDto, HookOrderTarget>(orders);

        // Assert - data mapped, hooks now fire in batch mode (Bug 3 fix)
        results.Should().HaveCount(2);
        results[0].OrderNumber.Should().Be("ORD-1");
        results[0].TotalAmount.Should().Be(100m);
        results[0].Items.Should().HaveCount(1);
        results[1].OrderNumber.Should().Be("ORD-2");
        results[1].Items.Should().HaveCount(2);

        // Hooks fire in batch mode now
        HookOrderMapperConfig.OrderHookCount.Should().Be(2);
        results[0].GrandTotal.Should().Be(110m);  // 100 + 10
        results[0].ProcessedBy.Should().Be("ZMapper-HookTest");
        results[1].GrandTotal.Should().Be(200m);  // 200 + 0 (null shipping)
        results[1].ProcessedBy.Should().Be("ZMapper-HookTest");
    }
}
