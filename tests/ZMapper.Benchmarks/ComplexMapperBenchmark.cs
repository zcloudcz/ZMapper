using AutoMapper;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace ZMapper.Benchmarks;

/// <summary>
/// Benchmark for mapping complex objects with nested types and collections.
/// This simulates real-world e-commerce domain mapping (Order with Items, Customer with Address).
/// Compares: Manual mapping, ZMapper (source-generated), Mapperly, and AutoMapper.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public partial class ComplexMapperBenchmark
{
    // Source objects for each benchmark scenario
    private ComplexOrder _order = null!;
    private ComplexCustomer _customer = null!;

    // Mapper instances
    private IMapper _autoMapper = null!;
    private ZMapper.Abstractions.IMapper _zMapper = null!;
    private ComplexMapperlyMapper _mapperlyMapper = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create a realistic order with 5 items and nested status info
        _order = CreateTestOrder();

        // Create a customer with billing and shipping addresses
        _customer = CreateTestCustomer();

        // Setup AutoMapper with all complex type mappings
        var autoMapperConfig = new AutoMapper.MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ComplexAddress, ComplexAddressDto>();
            cfg.CreateMap<ComplexCustomer, ComplexCustomerDto>();
            cfg.CreateMap<ComplexOrderStatusInfo, ComplexOrderStatusInfoDto>();
            cfg.CreateMap<ComplexOrderItem, ComplexOrderItemDto>();
            cfg.CreateMap<ComplexOrder, ComplexOrderDto>();
        });
        _autoMapper = autoMapperConfig.CreateMapper();

        // Setup ZMapper (uses source-generated code)
        _zMapper = ZMapperConfig.CreateMapper();

        // Setup Mapperly
        _mapperlyMapper = new ComplexMapperlyMapper();
    }

    // =========================================================================
    // Order Mapping Benchmarks (nested status + collection of items)
    // =========================================================================

    /// <summary>
    /// Baseline: Hand-written mapping code. This is the theoretical performance ceiling.
    /// Maps Order → OrderDto including nested OrderStatusInfo and List of OrderItems.
    /// </summary>
    [Benchmark(Baseline = true)]
    public ComplexOrderDto ManualOrderMapping()
    {
        var dto = new ComplexOrderDto
        {
            Id = _order.Id,
            OrderNumber = _order.OrderNumber,
            OrderedAt = _order.OrderedAt,
            ShippedDate = _order.ShippedDate,
            TotalAmount = _order.TotalAmount,
            ShippingCost = _order.ShippingCost,
            PaymentMethod = _order.PaymentMethod,
            IsPaid = _order.IsPaid,
            TrackingNumber = _order.TrackingNumber,
            CustomerId = _order.CustomerId,
            CurrentStatus = new ComplexOrderStatusInfoDto
            {
                Id = _order.CurrentStatus.Id,
                Status = _order.CurrentStatus.Status,
                Note = _order.CurrentStatus.Note,
                ChangedBy = _order.CurrentStatus.ChangedBy,
                ChangedAt = _order.CurrentStatus.ChangedAt,
                IsTerminal = _order.CurrentStatus.IsTerminal
            }
        };

        // Manually map the Items collection
        var items = new List<ComplexOrderItemDto>(_order.Items.Count);
        foreach (var item in _order.Items)
        {
            items.Add(new ComplexOrderItemDto
            {
                Id = item.Id,
                ProductName = item.ProductName,
                ProductCode = item.ProductCode,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                TotalPrice = item.TotalPrice,
                IsGift = item.IsGift
            });
        }
        dto.Items = items;

        return dto;
    }

    /// <summary>
    /// ZMapper using generated extension method - should be near-manual speed.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto ZMapper_Order()
    {
        return _zMapper.Map<ComplexOrder, ComplexOrderDto>(_order);
    }

    /// <summary>
    /// Mapperly source-generated mapping for complex order.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto Mapperly_Order()
    {
        return _mapperlyMapper.MapOrder(_order);
    }

    /// <summary>
    /// AutoMapper runtime reflection-based mapping for complex order.
    /// </summary>
    [Benchmark]
    public ComplexOrderDto AutoMapper_Order()
    {
        return _autoMapper.Map<ComplexOrderDto>(_order);
    }

    // =========================================================================
    // Customer Mapping Benchmarks (nested Address objects)
    // =========================================================================

    /// <summary>
    /// Baseline: Hand-written customer mapping including nested addresses.
    /// </summary>
    [Benchmark]
    public ComplexCustomerDto ManualCustomerMapping()
    {
        var dto = new ComplexCustomerDto
        {
            Id = _customer.Id,
            FirstName = _customer.FirstName,
            LastName = _customer.LastName,
            Email = _customer.Email,
            Phone = _customer.Phone,
            DateOfBirth = _customer.DateOfBirth,
            LoyaltyPoints = _customer.LoyaltyPoints,
            CreditLimit = _customer.CreditLimit,
            IsActive = _customer.IsActive,
            CreatedAt = _customer.CreatedAt,
            ExternalSystemId = _customer.ExternalSystemId,
            BillingAddress = MapAddressManual(_customer.BillingAddress),
            ShippingAddress = _customer.ShippingAddress != null
                ? MapAddressManual(_customer.ShippingAddress)
                : null
        };
        return dto;
    }

    [Benchmark]
    public ComplexCustomerDto ZMapper_Customer()
    {
        return _zMapper.Map<ComplexCustomer, ComplexCustomerDto>(_customer);
    }

    [Benchmark]
    public ComplexCustomerDto Mapperly_Customer()
    {
        return _mapperlyMapper.MapCustomer(_customer);
    }

    [Benchmark]
    public ComplexCustomerDto AutoMapper_Customer()
    {
        return _autoMapper.Map<ComplexCustomerDto>(_customer);
    }

    // =========================================================================
    // Helper: manual address mapping (reused by customer manual mapping)
    // =========================================================================

    private static ComplexAddressDto MapAddressManual(ComplexAddress address)
    {
        return new ComplexAddressDto
        {
            Id = address.Id,
            Street = address.Street,
            City = address.City,
            ZipCode = address.ZipCode,
            Country = address.Country,
            State = address.State,
            Floor = address.Floor,
            ApartmentNumber = address.ApartmentNumber,
            Latitude = address.Latitude,
            Longitude = address.Longitude,
            IsDefault = address.IsDefault
        };
    }

    // =========================================================================
    // Test data factory methods
    // =========================================================================

    /// <summary>
    /// Creates a realistic order with 5 items and full status info.
    /// This represents a typical e-commerce order payload.
    /// </summary>
    private static ComplexOrder CreateTestOrder()
    {
        return new ComplexOrder
        {
            Id = 1001,
            OrderNumber = "ORD-2024-001",
            OrderedAt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            ShippedDate = new DateOnly(2024, 6, 17),
            TotalAmount = 549.95m,
            ShippingCost = 12.99m,
            PaymentMethod = PaymentMethod.CreditCard,
            IsPaid = true,
            TrackingNumber = "TRK-9876543210",
            CustomerId = Guid.NewGuid(),
            CurrentStatus = new ComplexOrderStatusInfo
            {
                Id = 1,
                Status = OrderStatus.Shipped,
                Note = "Package dispatched via express delivery",
                ChangedBy = "System",
                ChangedAt = new DateTime(2024, 6, 17, 8, 0, 0, DateTimeKind.Utc),
                IsTerminal = false
            },
            Items = new List<ComplexOrderItem>
            {
                new() { Id = 1, ProductName = "Wireless Mouse", ProductCode = "WM-100", Quantity = 2, UnitPrice = 29.99m, Discount = 5.00m, IsGift = false },
                new() { Id = 2, ProductName = "USB-C Hub", ProductCode = "HUB-200", Quantity = 1, UnitPrice = 79.99m, Discount = null, IsGift = false },
                new() { Id = 3, ProductName = "Keyboard", ProductCode = "KB-300", Quantity = 1, UnitPrice = 149.99m, Discount = 10.00m, IsGift = false },
                new() { Id = 4, ProductName = "Screen Protector", ProductCode = null, Quantity = 3, UnitPrice = 9.99m, Discount = null, IsGift = true },
                new() { Id = 5, ProductName = "Laptop Stand", ProductCode = "LS-500", Quantity = 1, UnitPrice = 249.99m, Discount = 25.00m, IsGift = false }
            }
        };
    }

    /// <summary>
    /// Creates a customer with both billing and shipping addresses set.
    /// Tests nested object mapping with nullable nested type.
    /// </summary>
    private static ComplexCustomer CreateTestCustomer()
    {
        return new ComplexCustomer
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1-555-0123",
            DateOfBirth = new DateOnly(1990, 5, 15),
            LoyaltyPoints = 1500,
            CreditLimit = 10000m,
            IsActive = true,
            CreatedAt = new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            ExternalSystemId = 987654321L,
            BillingAddress = new ComplexAddress
            {
                Id = 1,
                Street = "100 Billing Street",
                City = "Chicago",
                ZipCode = "60601",
                Country = "USA",
                State = "IL",
                Floor = "5",
                ApartmentNumber = "501",
                Latitude = 41.8781,
                Longitude = -87.6298,
                IsDefault = true
            },
            ShippingAddress = new ComplexAddress
            {
                Id = 2,
                Street = "200 Shipping Avenue",
                City = "New York",
                ZipCode = "10001",
                Country = "USA",
                State = "NY",
                Floor = null,
                ApartmentNumber = null,
                Latitude = 40.7128,
                Longitude = -74.0060,
                IsDefault = false
            }
        };
    }
}
