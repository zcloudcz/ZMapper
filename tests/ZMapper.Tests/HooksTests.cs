using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test classes for hooks (BeforeMap/AfterMap)
public class InvoiceDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class Invoice
{
    public int InvoiceId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
}

public partial class HooksMapperConfig
{
    private static int _beforeMapCallCount = 0;
    private static int _afterMapCallCount = 0;

    public static void ResetCounters()
    {
        _beforeMapCallCount = 0;
        _afterMapCallCount = 0;
    }

    public static int BeforeMapCallCount => _beforeMapCallCount;
    public static int AfterMapCallCount => _afterMapCallCount;

    public static void ConfigureMapper(MapperConfiguration config)
    {
        config.CreateMap<InvoiceDto, Invoice>()
            .ForMember(dest => dest.InvoiceId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Customer, opt => opt.MapFrom(src => src.CustomerName))
            .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Total))
            .IgnoreNonExisting() // CreatedAt, ProcessedBy are set by hooks
            .BeforeMap((src, dest) =>
            {
                _beforeMapCallCount++;
                dest.CreatedAt = DateTime.UtcNow;
            })
            .AfterMap((src, dest) =>
            {
                _afterMapCallCount++;
                dest.ProcessedBy = "ZMapper";
            });
    }

    public static IMapper CreateMapper()
    {
        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for BeforeMap and AfterMap hooks
/// </summary>
public class HooksTests
{
    private readonly IMapper _mapper;

    public HooksTests()
    {
        HooksMapperConfig.ResetCounters();
        _mapper = HooksMapperConfig.CreateMapper();
    }

    [Fact]
    public void Hooks_BeforeMapShouldBeCalledBeforeMapping()
    {
        // Arrange
        var dto = new InvoiceDto
        {
            Id = 1,
            CustomerName = "John Doe",
            Total = 99.99m
        };

        // Act
        var invoice = _mapper.Map<InvoiceDto, Invoice>(dto);

        // Assert
        invoice.Should().NotBeNull();
        invoice.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        HooksMapperConfig.BeforeMapCallCount.Should().Be(1);
    }

    [Fact]
    public void Hooks_AfterMapShouldBeCalledAfterMapping()
    {
        // Arrange
        var dto = new InvoiceDto
        {
            Id = 2,
            CustomerName = "Jane Smith",
            Total = 149.99m
        };

        // Act
        var invoice = _mapper.Map<InvoiceDto, Invoice>(dto);

        // Assert
        invoice.Should().NotBeNull();
        invoice.ProcessedBy.Should().Be("ZMapper");
        HooksMapperConfig.AfterMapCallCount.Should().Be(1);
    }

    [Fact]
    public void Hooks_BothHooksShouldBeCalledInOrder()
    {
        // Arrange
        var dto = new InvoiceDto
        {
            Id = 3,
            CustomerName = "Bob Wilson",
            Total = 249.99m
        };

        // Act
        var invoice = _mapper.Map<InvoiceDto, Invoice>(dto);

        // Assert - All properties should be mapped correctly
        invoice.Should().NotBeNull();
        invoice.InvoiceId.Should().Be(3);
        invoice.Customer.Should().Be("Bob Wilson");
        invoice.Amount.Should().Be(249.99m);

        // BeforeMap set CreatedAt
        invoice.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));

        // AfterMap set ProcessedBy
        invoice.ProcessedBy.Should().Be("ZMapper");

        // Both hooks called exactly once
        HooksMapperConfig.BeforeMapCallCount.Should().Be(1);
        HooksMapperConfig.AfterMapCallCount.Should().Be(1);
    }

    [Fact]
    public void Hooks_ShouldBeCalledForEachMappingInvocation()
    {
        // Arrange
        var dto1 = new InvoiceDto { Id = 4, CustomerName = "Alice", Total = 50m };
        var dto2 = new InvoiceDto { Id = 5, CustomerName = "Charlie", Total = 75m };

        // Act
        var invoice1 = _mapper.Map<InvoiceDto, Invoice>(dto1);
        var invoice2 = _mapper.Map<InvoiceDto, Invoice>(dto2);

        // Assert
        invoice1.ProcessedBy.Should().Be("ZMapper");
        invoice2.ProcessedBy.Should().Be("ZMapper");

        // Hooks should be called for each mapping
        HooksMapperConfig.BeforeMapCallCount.Should().Be(2);
        HooksMapperConfig.AfterMapCallCount.Should().Be(2);
    }
}
