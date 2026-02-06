
namespace ZMapper.Tests.Model;

// === Enums ===

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

public enum Gender
{
    Male,
    Female,
    Other,
    Unspecified
}

public enum PaymentMethod
{
    Cash,
    CreditCard,
    BankTransfer,
    PayPal,
    CryptoCurrency
}

// === Address ===

public class Address
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

// === Customer ===

public class Customer
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; }
    public float? Rating { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? ProfileImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public TimeSpan? AverageSessionDuration { get; set; }
    public Uri? WebsiteUrl { get; set; }
    public char? CustomerTier { get; set; }  // 'A', 'B', 'C'
    public short? PreferredLanguageCode { get; set; }
    public long ExternalSystemId { get; set; }

    public Address BillingAddress { get; set; } = new();
    public Address? ShippingAddress { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

// === Order Status Info ===

public class OrderStatusInfo
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public DateTime? EstimatedCompletionAt { get; set; }
    public TimeOnly? ProcessedAtTime { get; set; }
    public bool IsTerminal { get; set; }
    public uint? InternalCode { get; set; }
}

// === Order Item ===

public class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? Discount { get; set; }
    public double? Weight { get; set; }
    public float? TaxRate { get; set; }
    public bool IsGift { get; set; }
    public sbyte? WarrantyYears { get; set; }
    public ushort? StockQuantity { get; set; }
    public byte[]? Thumbnail { get; set; }

    public decimal TotalPrice => Quantity * UnitPrice - (Discount ?? 0m);

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
}

// === Order ===

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderedAt { get; set; }
    public DateOnly? ShippedDate { get; set; }
    public DateTimeOffset? DeliveredAtOffset { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? ShippingCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentMethod? AlternativePaymentMethod { get; set; }
    public bool IsPaid { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid? CouponId { get; set; }
    public long? TransactionId { get; set; }
    public nint? InternalHandle { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public OrderStatusInfo CurrentStatus { get; set; } = new();
    public ICollection<OrderStatusInfo> StatusHistory { get; set; } = new List<OrderStatusInfo>();
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
