namespace ZMapper.Benchmarks;

// =============================================================================
// Complex domain models copied from ZMapper.Tests.Model.CustomerOrder.cs
// These models represent a real-world e-commerce domain with nested objects,
// collections, enums, nullable types, and calculated properties.
// =============================================================================

// === Enums ===

/// <summary>
/// Represents the lifecycle status of an order.
/// </summary>
public enum OrderStatus
{
    Created,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Returned
}

/// <summary>
/// Payment method for an order.
/// </summary>
public enum PaymentMethod
{
    Cash,
    CreditCard,
    BankTransfer,
    PayPal,
    CryptoCurrency
}

// === Domain Models ===

/// <summary>
/// Address entity with nullable properties and geographic coordinates.
/// </summary>
public class ComplexAddress
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? Floor { get; set; }
    public string? ApartmentNumber { get; set; }
    public double Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Customer entity with nested Address, various nullable types (Guid, DateOnly,
/// decimal?, char?, long), and a collection of orders.
/// </summary>
public class ComplexCustomer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public long ExternalSystemId { get; set; }

    // Nested object - requires recursive mapping
    public ComplexAddress BillingAddress { get; set; } = new();
    public ComplexAddress? ShippingAddress { get; set; }
}

/// <summary>
/// Order status info with enum, DateTime, and nullable fields.
/// </summary>
public class ComplexOrderStatusInfo
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public bool IsTerminal { get; set; }
}

/// <summary>
/// Order item with calculated TotalPrice property, nullable discount, and various types.
/// </summary>
public class ComplexOrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public bool IsGift { get; set; }

    // Calculated property - tests that mappers handle read-only computed values
    public decimal TotalPrice => Quantity * UnitPrice - (Discount ?? 0m);
}

/// <summary>
/// Order entity - the most complex type: nested OrderStatusInfo, collection of OrderItems,
/// enum properties, nullable types, and Guid references.
/// </summary>
public class ComplexOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public DateOnly? ShippedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid CustomerId { get; set; }

    // Nested object - single
    public ComplexOrderStatusInfo CurrentStatus { get; set; } = new();

    // Nested collection - requires element-by-element mapping
    public ICollection<ComplexOrderItem> Items { get; set; } = new List<ComplexOrderItem>();
}

// === DTO Models ===

/// <summary>
/// Flattened address DTO - mirrors the domain model for 1:1 mapping benchmark.
/// </summary>
public class ComplexAddressDto
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? Floor { get; set; }
    public string? ApartmentNumber { get; set; }
    public double Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Customer DTO with nested AddressDto - tests nested object mapping performance.
/// </summary>
public class ComplexCustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ExternalSystemId { get; set; }
    public ComplexAddressDto BillingAddress { get; set; } = new();
    public ComplexAddressDto? ShippingAddress { get; set; }
}

/// <summary>
/// Order status DTO - simplified version of domain model.
/// </summary>
public class ComplexOrderStatusInfoDto
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public bool IsTerminal { get; set; }
}

/// <summary>
/// Order item DTO - TotalPrice is a regular property (not calculated) in the DTO.
/// </summary>
public class ComplexOrderItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsGift { get; set; }
}

/// <summary>
/// Order DTO with nested status and collection of items - the full complex mapping scenario.
/// </summary>
public class ComplexOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public DateOnly? ShippedDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid CustomerId { get; set; }
    public ComplexOrderStatusInfoDto CurrentStatus { get; set; } = new();
    public ICollection<ComplexOrderItemDto> Items { get; set; } = [];
}
