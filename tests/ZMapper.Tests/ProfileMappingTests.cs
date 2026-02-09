using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Model classes for profile tests (prefixed to avoid name conflicts)
// ============================================================================

public class ProfileEmployeeDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class ProfileEmployee
{
    public int EmployeeId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public class ProfileInvoiceDto
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

public class ProfileInvoice
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

// ============================================================================
// Profile classes implementing IMapperProfile
// ============================================================================

/// <summary>
/// Profile that defines ProfileEmployeeDto -> ProfileEmployee mapping.
/// Demonstrates ForMember usage within a profile.
/// </summary>
public class EmployeeProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<ProfileEmployeeDto, ProfileEmployee>()
            .ForMember(dest => dest.EmployeeId, opt => opt.MapFrom(src => src.Id));
    }
}

/// <summary>
/// Profile that defines ProfileInvoiceDto -> ProfileInvoice mapping.
/// Demonstrates simple convention-based mapping within a profile.
/// </summary>
public class InvoiceProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        config.CreateMap<ProfileInvoiceDto, ProfileInvoice>();
    }
}

// ============================================================================
// Tests
// ============================================================================

/// <summary>
/// Tests for the IMapperProfile-based configuration pattern.
///
/// These tests verify that:
/// 1. Profiles generate a unified mapper (Mapper)
/// 2. The unified mapper handles mappings from multiple profiles
/// 3. ForMember configurations within profiles work correctly
/// 4. The Create() and Create(config) factory methods work
/// </summary>
public class ProfileMappingTests
{
    [Fact]
    public void UnifiedMapper_ShouldMapEmployeeDto_ToEmployee()
    {
        // Arrange - Create unified mapper directly (without DI)
        var mapper = Mapper.Create();

        var dto = new ProfileEmployeeDto
        {
            Id = 42,
            FirstName = "John",
            LastName = "Doe",
            Department = "Engineering"
        };

        // Act
        var result = mapper.Map<ProfileEmployeeDto, ProfileEmployee>(dto);

        // Assert
        result.EmployeeId.Should().Be(42); // ForMember: Id -> EmployeeId
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Department.Should().Be("Engineering");
    }

    [Fact]
    public void UnifiedMapper_ShouldMapInvoiceDto_ToInvoice()
    {
        // Arrange
        var mapper = Mapper.Create();

        var dto = new ProfileInvoiceDto
        {
            InvoiceId = 100,
            Amount = 999.99m,
            CustomerName = "Acme Corp"
        };

        // Act
        var result = mapper.Map<ProfileInvoiceDto, ProfileInvoice>(dto);

        // Assert
        result.InvoiceId.Should().Be(100);
        result.Amount.Should().Be(999.99m);
        result.CustomerName.Should().Be("Acme Corp");
    }

    [Fact]
    public void UnifiedMapper_ShouldMapArray()
    {
        // Arrange
        var mapper = Mapper.Create();

        var dtos = new[]
        {
            new ProfileInvoiceDto { InvoiceId = 1, Amount = 100m, CustomerName = "A" },
            new ProfileInvoiceDto { InvoiceId = 2, Amount = 200m, CustomerName = "B" }
        };

        // Act
        var result = mapper.MapArray<ProfileInvoiceDto, ProfileInvoice>(dtos);

        // Assert
        result.Should().HaveCount(2);
        result[0].InvoiceId.Should().Be(1);
        result[1].Amount.Should().Be(200m);
    }

    [Fact]
    public void UnifiedMapper_ShouldMapList()
    {
        // Arrange
        var mapper = Mapper.Create();

        var dtos = new List<ProfileEmployeeDto>
        {
            new() { Id = 1, FirstName = "Alice", LastName = "A", Department = "HR" },
            new() { Id = 2, FirstName = "Bob", LastName = "B", Department = "IT" }
        };

        // Act
        var result = mapper.MapList<ProfileEmployeeDto, ProfileEmployee>(dtos);

        // Assert
        result.Should().HaveCount(2);
        result[0].EmployeeId.Should().Be(1);
        result[0].FirstName.Should().Be("Alice");
        result[1].EmployeeId.Should().Be(2);
    }

    [Fact]
    public void UnifiedMapper_ShouldMapEnumerable()
    {
        // Arrange
        var mapper = Mapper.Create();

        IEnumerable<ProfileInvoiceDto> dtos = new List<ProfileInvoiceDto>
        {
            new() { InvoiceId = 10, Amount = 50m, CustomerName = "Test" }
        }.Where(i => i.Amount > 0);

        // Act
        var result = mapper.MapList<ProfileInvoiceDto, ProfileInvoice>(dtos);

        // Assert
        result.Should().HaveCount(1);
        result[0].InvoiceId.Should().Be(10);
    }

    [Fact]
    public void UnifiedMapper_ShouldMapToExistingObject()
    {
        // Arrange
        var mapper = Mapper.Create();

        var dto = new ProfileEmployeeDto { Id = 5, FirstName = "Updated", LastName = "Name", Department = "Sales" };
        var existing = new ProfileEmployee { EmployeeId = 99, FirstName = "Old", LastName = "Old", Department = "Old" };

        // Act
        var result = mapper.Map<ProfileEmployeeDto, ProfileEmployee>(dto, existing);

        // Assert
        result.Should().BeSameAs(existing);
        result.EmployeeId.Should().Be(5);
        result.FirstName.Should().Be("Updated");
    }

    [Fact]
    public void UnifiedMapper_CreateWithConfig_ShouldWork()
    {
        // Arrange - Create via config (manually calling profiles)
        var config = new MapperConfiguration();
        new EmployeeProfile().Configure(config);
        new InvoiceProfile().Configure(config);

        // Act
        var mapper = Mapper.Create(config);

        var dto = new ProfileEmployeeDto { Id = 1, FirstName = "Test", LastName = "User", Department = "QA" };
        var result = mapper.Map<ProfileEmployeeDto, ProfileEmployee>(dto);

        // Assert
        result.EmployeeId.Should().Be(1);
        result.FirstName.Should().Be("Test");
    }

    [Fact]
    public void UnifiedMapper_UnsupportedMapping_ShouldThrow()
    {
        // Arrange
        var mapper = Mapper.Create();

        // Act & Assert - Mapping that's not configured should throw
        var act = () => mapper.Map<string, int>("test");

        act.Should().Throw<NotSupportedException>()
           .WithMessage("*not configured*");
    }

    [Fact]
    public void Profile_CanBeUsedManually_WithoutDI()
    {
        // Arrange - Users can use profiles without DI by calling Configure manually
        var config = new MapperConfiguration();

        // Apply profiles manually
        var employeeProfile = new EmployeeProfile();
        var invoiceProfile = new InvoiceProfile();
        employeeProfile.Configure(config);
        invoiceProfile.Configure(config);

        // Verify config has mappings registered
        config.Mappings.Should().HaveCount(2);
        config.Mappings.Should().Contain(m => m.SourceType == typeof(ProfileEmployeeDto));
        config.Mappings.Should().Contain(m => m.SourceType == typeof(ProfileInvoiceDto));
    }
}
