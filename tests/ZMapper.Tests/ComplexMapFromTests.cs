using FluentAssertions;
using ZMapper.Abstractions;
using Xunit;

namespace ZMapper.Tests;

// === Test models for complex MapFrom expressions ===

/// <summary>
/// Simulates an entity with a navigation property (nested object).
/// This is the typical EF Core pattern: Invoice -> Client -> CompanyName.
/// </summary>
public class MapFromClientInfo
{
    public string CompanyName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
}

public class MapFromInvoiceEntity
{
    public int Id { get; set; }
    public DateTime? IssueDate { get; set; }
    public decimal Amount { get; set; }
    public MapFromClientInfo? Client { get; set; }
}

/// <summary>
/// Flat DTO that needs data from nested navigation properties and expressions.
/// </summary>
public class MapFromInvoiceDto
{
    public int Id { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal Amount { get; set; }
    public string ClientName { get; set; } = "";
}

// === Mapper configuration ===

public partial class ComplexMapFromConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        config.CreateMap<MapFromInvoiceEntity, MapFromInvoiceDto>()
            // Complex expression: null-coalescing on nullable DateTime
            .ForMember(dest => dest.IssueDate, opt => opt.MapFrom(src => src.IssueDate ?? DateTime.MinValue))
            // Navigation property: flatten nested object access
            .ForMember(dest => dest.ClientName, opt => opt.MapFrom(src => src.Client != null ? src.Client.CompanyName : ""));

        return CreateGeneratedMapper();
    }
}

// === Tests ===

public class ComplexMapFromTests
{
    private readonly IMapper _mapper = ComplexMapFromConfig.CreateMapper();

    /// <summary>
    /// Verifies that MapFrom with a null-coalescing expression works correctly.
    /// Previously, complex expressions in MapFrom were silently ignored (Issue #4).
    /// </summary>
    [Fact]
    public void Should_Apply_NullCoalescing_MapFrom_Expression()
    {
        var entity = new MapFromInvoiceEntity
        {
            Id = 1,
            IssueDate = new DateTime(2025, 6, 15),
            Amount = 1000m,
            Client = new MapFromClientInfo { CompanyName = "Acme Corp" }
        };

        var dto = _mapper.Map<MapFromInvoiceEntity, MapFromInvoiceDto>(entity);

        dto.Id.Should().Be(1);
        dto.IssueDate.Should().Be(new DateTime(2025, 6, 15));
        dto.Amount.Should().Be(1000m);
    }

    /// <summary>
    /// When the nullable source property is null, the expression should provide the fallback.
    /// </summary>
    [Fact]
    public void Should_Apply_Fallback_When_Source_Is_Null()
    {
        var entity = new MapFromInvoiceEntity
        {
            Id = 2,
            IssueDate = null, // null source -> should use DateTime.MinValue
            Amount = 500m,
            Client = null
        };

        var dto = _mapper.Map<MapFromInvoiceEntity, MapFromInvoiceDto>(entity);

        dto.Id.Should().Be(2);
        dto.IssueDate.Should().Be(DateTime.MinValue, "null-coalescing should provide DateTime.MinValue");
        dto.ClientName.Should().Be("", "ternary expression should provide empty string when Client is null");
    }

    /// <summary>
    /// Navigation property flattening: src.Client.CompanyName -> dest.ClientName.
    /// </summary>
    [Fact]
    public void Should_Map_Navigation_Property()
    {
        var entity = new MapFromInvoiceEntity
        {
            Id = 3,
            IssueDate = new DateTime(2025, 1, 1),
            Amount = 750m,
            Client = new MapFromClientInfo
            {
                CompanyName = "ZCloud s.r.o.",
                ContactEmail = "info@zcloud.cz"
            }
        };

        var dto = _mapper.Map<MapFromInvoiceEntity, MapFromInvoiceDto>(entity);

        dto.ClientName.Should().Be("ZCloud s.r.o.", "navigation property should be flattened");
    }
}
