using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for MemberList validation (Feature 7) and Standalone Mapper (Feature 8)
// ============================================================================

public class UserFull
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty; // Not in DTO — source-only property
}

public class UserSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CloneableEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class ClonedEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

// ============================================================================
// Profile for Feature 7 + 8 tests
// ============================================================================
public partial class MemberListTestProfile : IMapperProfile
{
    public void Configure(MapperConfiguration config)
    {
        // MemberList.None — no warnings for unmapped properties on either side
        config.CreateMap<UserFull, UserSummary>(MemberList.None);

        // Standard mapping for standalone mapper test
        config.CreateMap<CloneableEntity, ClonedEntity>();
    }
}

// ============================================================================
// Tests for MemberList validation (Feature 7)
// ============================================================================
public class MemberListTests
{
    [Fact]
    public void MemberListNone_ShouldMapMatchingProperties()
    {
        // Arrange — MemberList.None suppresses all diagnostics
        // Email and InternalCode on source have no match on destination, but no warning
        var source = new UserFull
        {
            Id = 1,
            Name = "Alice",
            Email = "alice@example.com",
            InternalCode = "X-123"
        };

        // Act
        var result = source.ToUserSummary();

        // Assert — only matching properties are mapped
        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
    }

    [Fact]
    public void MemberListDestination_IsDefault_ShouldMapProperties()
    {
        // Arrange — default MemberList.Destination behavior
        var source = new CloneableEntity { Id = 42, Label = "Test" };

        // Act
        var result = source.ToClonedEntity();

        // Assert
        result.Id.Should().Be(42);
        result.Label.Should().Be("Test");
    }
}

// ============================================================================
// Tests for Standalone Mapper (Feature 8)
// ============================================================================
public class StandaloneMapperTests
{
    [Fact]
    public void CreateMapper_ShouldCreateStandaloneMapper()
    {
        // Arrange — create mapper without DI, using config.CreateMapper()
        var config = new MapperConfiguration();
        new MemberListTestProfile().Configure(config);

        // Act
        var mapper = config.CreateMapper();

        // Assert — mapper should work
        var source = new CloneableEntity { Id = 99, Label = "Standalone" };
        var result = mapper.Map<CloneableEntity, ClonedEntity>(source);

        result.Id.Should().Be(99);
        result.Label.Should().Be("Standalone");
    }

    [Fact]
    public void MapperCreate_Static_ShouldWork()
    {
        // Arrange — use Mapper.Create() directly
        var mapper = Mapper.Create();

        // Act
        var source = new UserFull
        {
            Id = 7,
            Name = "Bob",
            Email = "bob@test.com",
            InternalCode = "Y-456"
        };
        var result = mapper.Map<UserFull, UserSummary>(source);

        // Assert
        result.Id.Should().Be(7);
        result.Name.Should().Be("Bob");
    }

    [Fact]
    public void MapperCreate_WithConfig_ShouldWireHooks()
    {
        // Arrange
        var config = new MapperConfiguration();
        new MemberListTestProfile().Configure(config);
        var mapper = Mapper.Create(config);

        // Act
        var source = new CloneableEntity { Id = 55, Label = "Hooked" };
        var result = mapper.Map<CloneableEntity, ClonedEntity>(source);

        // Assert
        result.Id.Should().Be(55);
        result.Label.Should().Be("Hooked");
    }
}
