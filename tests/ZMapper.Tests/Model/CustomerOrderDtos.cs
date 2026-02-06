namespace ZMapper.Tests.Model;

// === Address DTO ===
public class AddressDto
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

// === Customer DTO ===
public class CustomerDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long ExternalSystemId { get; set; }
    public AddressDto BillingAddress { get; set; } = new();
    public AddressDto? ShippingAddress { get; set; }
}

// === Order Status Info DTO ===
public class OrderStatusInfoDto
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public string? Note { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public bool IsTerminal { get; set; }
}

// === Order Item DTO ===
public class OrderItemDto
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

// === Order DTO ===
public class OrderDto
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
    public OrderStatusInfoDto CurrentStatus { get; set; } = new();
    public ICollection<OrderItemDto> Items { get; set; } = [];
}