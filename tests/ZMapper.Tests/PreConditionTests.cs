using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for PreCondition + MappingContext (Features 4 & 5)
// ============================================================================

/// <summary>Company DTO with nested navigation properties.</summary>
public class CompanyDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? RegionName { get; set; }
    public string? StreamName { get; set; }
}

/// <summary>Company entity — Region/Stream only mapped when IgnoreNested is not set.</summary>
public class CompanyEntity
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? RegionName { get; set; }
    public string? StreamName { get; set; }
}

/// <summary>DTO with an optional "detail" field that should only map when context allows.</summary>
public class DetailItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>Entity with all fields writable.</summary>
public class DetailItemEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

// ============================================================================
// Mapper configuration with PreCondition
// ============================================================================
public partial class PreConditionMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // PreCondition: Only map RegionName and StreamName when context does NOT have IgnoreNested=true
        config.CreateMap<CompanyDto, CompanyEntity>()
            .ForMember(d => d.RegionName,
                opt => opt.PreCondition((src, ctx) => !ctx.GetOrDefault<bool>("IgnoreNested")))
            .ForMember(d => d.StreamName,
                opt => opt.PreCondition((src, ctx) => !ctx.GetOrDefault<bool>("IgnoreNested")));

        // PreCondition: Only map Detail when context has "IncludeDetail"=true
        config.CreateMap<DetailItemDto, DetailItemEntity>()
            .ForMember(d => d.Detail,
                opt => opt.PreCondition((src, ctx) => ctx.GetOrDefault<bool>("IncludeDetail")));

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================
public class PreConditionTests
{
    private readonly IMapper _mapper;

    public PreConditionTests()
    {
        _mapper = PreConditionMapperConfig.CreateMapper();
    }

    [Fact]
    public void PreCondition_WithoutContext_ShouldMapAllProperties()
    {
        // Arrange — no context = all properties mapped normally
        var dto = new CompanyDto
        {
            CompanyId = 1,
            CompanyName = "Acme",
            RegionName = "Europe",
            StreamName = "Enterprise"
        };

        // Act — map without context (regular Map<S,D>)
        var result = _mapper.Map<CompanyDto, CompanyEntity>(dto);

        // Assert — all properties should be mapped
        result.CompanyId.Should().Be(1);
        result.CompanyName.Should().Be("Acme");
        result.RegionName.Should().Be("Europe");
        result.StreamName.Should().Be("Enterprise");
    }

    [Fact]
    public void PreCondition_WithIgnoreNestedTrue_ShouldSkipNestedProperties()
    {
        // Arrange
        var dto = new CompanyDto
        {
            CompanyId = 2,
            CompanyName = "Widget Corp",
            RegionName = "Asia",
            StreamName = "Startup"
        };

        // Act — pass IgnoreNested=true in context
        var context = new MappingContext();
        context["IgnoreNested"] = true;
        var result = _mapper.Map<CompanyDto, CompanyEntity>(dto, context);

        // Assert — RegionName and StreamName should NOT be mapped
        result.CompanyId.Should().Be(2);
        result.CompanyName.Should().Be("Widget Corp");
        result.RegionName.Should().BeNull("PreCondition should prevent mapping when IgnoreNested is true");
        result.StreamName.Should().BeNull("PreCondition should prevent mapping when IgnoreNested is true");
    }

    [Fact]
    public void PreCondition_WithIgnoreNestedFalse_ShouldMapNestedProperties()
    {
        // Arrange
        var dto = new CompanyDto
        {
            CompanyId = 3,
            CompanyName = "TestCo",
            RegionName = "Americas",
            StreamName = "Growth"
        };

        // Act — explicitly set IgnoreNested=false
        var context = new MappingContext();
        context["IgnoreNested"] = false;
        var result = _mapper.Map<CompanyDto, CompanyEntity>(dto, context);

        // Assert — all properties mapped
        result.CompanyId.Should().Be(3);
        result.CompanyName.Should().Be("TestCo");
        result.RegionName.Should().Be("Americas");
        result.StreamName.Should().Be("Growth");
    }

    [Fact]
    public void PreCondition_IncludeDetail_True_ShouldMapDetail()
    {
        // Arrange
        var dto = new DetailItemDto { Id = 10, Name = "Widget", Detail = "Detailed info" };

        // Act
        var context = new MappingContext();
        context["IncludeDetail"] = true;
        var result = _mapper.Map<DetailItemDto, DetailItemEntity>(dto, context);

        // Assert
        result.Id.Should().Be(10);
        result.Name.Should().Be("Widget");
        result.Detail.Should().Be("Detailed info");
    }

    [Fact]
    public void PreCondition_IncludeDetail_False_ShouldSkipDetail()
    {
        // Arrange
        var dto = new DetailItemDto { Id = 11, Name = "Gadget", Detail = "Secret info" };

        // Act
        var context = new MappingContext();
        context["IncludeDetail"] = false;
        var result = _mapper.Map<DetailItemDto, DetailItemEntity>(dto, context);

        // Assert
        result.Id.Should().Be(11);
        result.Name.Should().Be("Gadget");
        result.Detail.Should().BeNull("PreCondition should prevent mapping when IncludeDetail is false");
    }

    [Fact]
    public void PreCondition_EmptyContext_ShouldUseDefaults()
    {
        // Arrange — empty context, GetOrDefault<bool> returns false
        var dto = new DetailItemDto { Id = 12, Name = "Thing", Detail = "Info" };

        // Act
        var context = new MappingContext();
        var result = _mapper.Map<DetailItemDto, DetailItemEntity>(dto, context);

        // Assert — IncludeDetail defaults to false, so Detail is not mapped
        result.Id.Should().Be(12);
        result.Name.Should().Be("Thing");
        result.Detail.Should().BeNull("default for GetOrDefault<bool> is false");
    }

    [Fact]
    public void MappingContext_GetOrDefault_ShouldReturnDefault()
    {
        // Test MappingContext utility methods directly
        var ctx = new MappingContext();

        ctx.GetOrDefault<bool>("Missing").Should().BeFalse();
        ctx.GetOrDefault("Missing", 42).Should().Be(42);
        ctx.GetOrDefault<string>("Missing", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void MappingContext_SetAndGet_ShouldWork()
    {
        var ctx = new MappingContext();
        ctx["key"] = "value";

        ctx.ContainsKey("key").Should().BeTrue();
        ctx.Get<string>("key").Should().Be("value");

        ctx.TryGetValue("key", out var val).Should().BeTrue();
        val.Should().Be("value");

        ctx.TryGetValue("missing", out _).Should().BeFalse();
    }
}
