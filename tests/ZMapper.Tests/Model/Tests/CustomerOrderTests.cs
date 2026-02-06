using Xunit;

namespace ZMapper.Tests.Model.Tests;

public class AddressTests
{
    [Fact]
    public void Address_DefaultValues_ShouldBeInitialized()
    {
        var address = new Address();

        Assert.Equal(string.Empty, address.Street);
        Assert.Equal(string.Empty, address.City);
        Assert.Equal(string.Empty, address.ZipCode);
        Assert.Equal(string.Empty, address.Country);
        Assert.Null(address.State);
        Assert.False(address.IsDefault);
    }

    [Fact]
    public void Address_SetProperties_ShouldRetainValues()
    {
        var address = new Address
        {
            Id = 1,
            Street = "123 Main St",
            City = "Seattle",
            ZipCode = "98101",
            Country = "USA",
            State = "WA",
            Latitude = 47.6062,
            Longitude = -122.3321,
            IsDefault = true
        };

        Assert.Equal(1, address.Id);
        Assert.Equal("123 Main St", address.Street);
        Assert.Equal("Seattle", address.City);
        Assert.Equal("WA", address.State);
        Assert.Equal(47.6062, address.Latitude);
        Assert.True(address.IsDefault);
    }
}

public class CustomerTests
{
    [Fact]
    public void Customer_DefaultValues_ShouldBeInitialized()
    {
        var customer = new Customer();

        Assert.Equal(Guid.Empty, customer.Id);
        Assert.Equal(string.Empty, customer.FirstName);
        Assert.Equal(string.Empty, customer.Email);
        Assert.True(customer.IsActive);
        Assert.NotNull(customer.BillingAddress);
        Assert.NotNull(customer.Orders);
    }

    [Fact]
    public void Customer_WithFullData_ShouldRetainAllValues()
    {
        var customerId = Guid.NewGuid();
        var billingAddress = new Address
        {
            Id = 1,
            Street = "123 Main St",
            City = "Seattle",
            ZipCode = "98101",
            Country = "USA",
            State = "WA",
            Latitude = 47.6062,
            Longitude = -122.3321,
            IsDefault = true
        };

        var shippingAddress = new Address
        {
            Id = 2,
            Street = "456 Oak Ave",
            City = "Portland",
            ZipCode = "97201",
            Country = "USA",
            State = "OR",
            Floor = "3",
            ApartmentNumber = "301"
        };

        var order = new Order
        {
            Id = 1,
            OrderNumber = "ORD-001",
            OrderedAt = DateTime.UtcNow,
            TotalAmount = 150.00m,
            PaymentMethod = PaymentMethod.CreditCard,
            IsPaid = true,
            CurrentStatus = new OrderStatusInfo
            {
                Id = 1,
                Status = OrderStatus.Confirmed,
                ChangedBy = "System",
                ChangedAt = DateTime.UtcNow
            },
            Items =
            [
                new OrderItem { Id = 1, ProductName = "Widget", Quantity = 2, UnitPrice = 50m },
                new OrderItem { Id = 2, ProductName = "Gadget", Quantity = 1, UnitPrice = 50m }
            ]
        };

        var customer = new Customer
        {
            Id = customerId,
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "+1234567890",
            DateOfBirth = new DateOnly(1990, 5, 15),
            Gender = Model.Gender.Male,
            LoyaltyPoints = 500,
            CreditLimit = 10000m,
            CustomerTier = 'A',
            ExternalSystemId = 123456789L,
            BillingAddress = billingAddress,
            ShippingAddress = shippingAddress,
            Orders = [order]
        };

        Assert.Equal(customerId, customer.Id);
        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal(Model.Gender.Male, customer.Gender);
        Assert.Equal(500, customer.LoyaltyPoints);
        Assert.Equal('A', customer.CustomerTier);
        Assert.Equal("123 Main St", customer.BillingAddress.Street);
        Assert.Equal("456 Oak Ave", customer.ShippingAddress?.Street);
        Assert.Single(customer.Orders);
    }

    [Fact]
    public void Customer_NullableProperties_ShouldAcceptNull()
    {
        var customer = new Customer
        {
            ShippingAddress = null,
            CreditLimit = null,
            Gender = null,
            LastLoginAt = null
        };

        Assert.Null(customer.ShippingAddress);
        Assert.Null(customer.CreditLimit);
        Assert.Null(customer.Gender);
        Assert.Null(customer.LastLoginAt);
    }
}

public class OrderItemTests
{
    [Fact]
    public void OrderItem_TotalPrice_ShouldCalculateCorrectly()
    {
        var item = new OrderItem
        {
            Quantity = 3,
            UnitPrice = 25.00m,
            Discount = 5.00m
        };

        Assert.Equal(70.00m, item.TotalPrice);
    }

    [Fact]
    public void OrderItem_TotalPrice_WithNoDiscount_ShouldCalculateCorrectly()
    {
        var item = new OrderItem
        {
            Quantity = 2,
            UnitPrice = 50.00m,
            Discount = null
        };

        Assert.Equal(100.00m, item.TotalPrice);
    }

    [Fact]
    public void OrderItem_DefaultValues_ShouldBeInitialized()
    {
        var item = new OrderItem();

        Assert.Equal(string.Empty, item.ProductName);
        Assert.Null(item.ProductCode);
        Assert.False(item.IsGift);
        Assert.Equal(0m, item.TotalPrice);
    }
}

public class OrderTests
{
    [Fact]
    public void Order_DefaultValues_ShouldBeInitialized()
    {
        var order = new Order();

        Assert.Equal(string.Empty, order.OrderNumber);
        Assert.NotNull(order.CurrentStatus);
        Assert.NotNull(order.StatusHistory);
        Assert.NotNull(order.Items);
        Assert.Empty(order.Items);
        Assert.False(order.IsPaid);
    }

    [Fact]
    public void Order_WithItems_ShouldContainItems()
    {
        var order = new Order
        {
            Id = 1,
            OrderNumber = "ORD-001",
            Items =
            [
                new OrderItem { Id = 1, ProductName = "Product A", Quantity = 2, UnitPrice = 10m },
                new OrderItem { Id = 2, ProductName = "Product B", Quantity = 1, UnitPrice = 25m }
            ]
        };

        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Order_PaymentMethod_ShouldAcceptAllValues()
    {
        var order = new Order { PaymentMethod = PaymentMethod.CreditCard };
        Assert.Equal(PaymentMethod.CreditCard, order.PaymentMethod);

        order.AlternativePaymentMethod = PaymentMethod.PayPal;
        Assert.Equal(PaymentMethod.PayPal, order.AlternativePaymentMethod);
    }
}

public class OrderStatusInfoTests
{
    [Fact]
    public void OrderStatusInfo_DefaultValues_ShouldBeInitialized()
    {
        var statusInfo = new OrderStatusInfo();

        Assert.Equal(OrderStatus.Created, statusInfo.Status);
        Assert.Equal(string.Empty, statusInfo.ChangedBy);
        Assert.False(statusInfo.IsTerminal);
    }

    [Fact]
    public void OrderStatusInfo_AllStatuses_ShouldBeValid()
    {
        var statuses = Enum.GetValues<OrderStatus>();

        Assert.Equal(7, statuses.Length);
        Assert.Contains(OrderStatus.Shipped, statuses);
        Assert.Contains(OrderStatus.Cancelled, statuses);
    }
}