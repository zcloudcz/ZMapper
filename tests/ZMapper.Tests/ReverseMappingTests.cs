using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for ReverseMap
public class OrderDto
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public partial class ReverseMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        config.CreateMap<OrderDto, Order>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.OrderId))
            .ForMember(dest => dest.Customer, opt => opt.MapFrom(src => src.CustomerName))
            .ForMember(dest => dest.Total, opt => opt.MapFrom(src => src.TotalAmount))
            .ReverseMap(); // Creates Order -> OrderDto mapping automatically

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for ReverseMap() functionality - bidirectional mapping
/// </summary>
public class ReverseMappingTests
{
    private readonly IMapper _mapper;

    public ReverseMappingTests()
    {
        _mapper = ReverseMapperConfig.CreateMapper();
    }

    [Fact]
    public void ForwardMapping_ShouldWork()
    {
        // Arrange
        var dto = new OrderDto
        {
            OrderId = 123,
            CustomerName = "John Smith",
            TotalAmount = 99.99m
        };

        // Act
        var order = _mapper.Map<OrderDto, Order>(dto);

        // Assert
        order.Should().NotBeNull();
        order.Id.Should().Be(123);
        order.Customer.Should().Be("John Smith");
        order.Total.Should().Be(99.99m);
    }

    [Fact]
    public void ReverseMapping_ShouldWork()
    {
        // Arrange
        var order = new Order
        {
            Id = 456,
            Customer = "Jane Doe",
            Total = 199.99m
        };

        // Act
        var dto = _mapper.Map<Order, OrderDto>(order);

        // Assert
        dto.Should().NotBeNull();
        dto.OrderId.Should().Be(456);
        dto.CustomerName.Should().Be("Jane Doe");
        dto.TotalAmount.Should().Be(199.99m);
    }
}
