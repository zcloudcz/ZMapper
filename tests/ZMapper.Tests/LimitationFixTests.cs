using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for Limitation #1: IgnoreNonExisting + ReverseMap + Inheritance
// ============================================================================

/// <summary>
/// Base entity with common properties (Id, CreatedAt).
/// Simulates a typical EF Core base entity pattern.
/// </summary>
public abstract class LimBaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Derived entity with its own properties + extra property not on DTO.
/// The extra InternalCode property exists only on the entity side.
/// </summary>
public class LimProductEntity : LimBaseEntity
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string InternalCode { get; set; } = ""; // Not on DTO
}

/// <summary>
/// Base DTO mirroring the base entity.
/// </summary>
public abstract class LimBaseDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Derived DTO with its own properties + extra property not on entity.
/// The extra DisplayLabel property exists only on the DTO side.
/// </summary>
public class LimProductDto : LimBaseDto
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string DisplayLabel { get; set; } = "default"; // Not on entity
}

// ============================================================================
// Test models for Limitation #3: Nullable in object initializer path
// ============================================================================

/// <summary>
/// Source with nullable value types. These properties may be null in the database.
/// </summary>
public class NullableInitSource
{
    public int Id { get; set; }
    public DateTime? EventDate { get; set; }
    public int? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Destination with required non-nullable value types.
/// The 'required' keyword forces the object initializer path in the generator,
/// which previously did NOT apply ?? default for nullable-to-non-nullable conversions.
/// </summary>
public class RequiredInitDest
{
    public required int Id { get; set; }
    public required DateTime EventDate { get; set; }
    public required int Quantity { get; set; }
    public required decimal Amount { get; set; }
    public required string Name { get; set; }
}

// ============================================================================
// Mapper configuration covering all three limitation fixes
// ============================================================================

/// <summary>
/// Configuration that registers mappings for limitation fix tests.
/// Tests: IgnoreNonExisting + ReverseMap + Inheritance, and nullable in object initializer.
/// </summary>
public partial class LimitationFixConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Limitation #1: IgnoreNonExisting + ReverseMap with inherited properties.
        // Both sides have extra properties (InternalCode on entity, DisplayLabel on DTO).
        // IgnoreNonExisting suppresses ZMAP001 for non-matching properties.
        // ReverseMap creates the reverse direction and must propagate IgnoreNonExisting.
        config.CreateMap<LimProductDto, LimProductEntity>()
            .IgnoreNonExisting()
            .ReverseMap();

        // Limitation #3: Nullable source → non-nullable required destination.
        // The 'required' keyword triggers the object initializer code path,
        // which must use ?? default to avoid CS0266 compile error.
        config.CreateMap<NullableInitSource, RequiredInitDest>()
            .IgnoreNonExisting();

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================

/// <summary>
/// Tests verifying fixes for Known Limitations #1, #2, and #3.
///
/// Limitation #1: IgnoreNonExisting now works correctly with inherited properties
///                and propagates to ReverseMap.
/// Limitation #2: ZMAP002 diagnostic fires for non-partial profile classes
///                (verified at build time — the build itself is the test).
/// Limitation #3: Nullable value types in object initializer path now use ?? default.
/// </summary>
public class LimitationFixTests
{
    private readonly IMapper _mapper = LimitationFixConfig.CreateMapper();

    // ---- Limitation #1: IgnoreNonExisting + Inheritance ----

    /// <summary>
    /// Verifies that inherited properties (Id, CreatedAt from LimBaseEntity/LimBaseDto)
    /// are correctly mapped when IgnoreNonExisting() is used.
    /// This was the original bug: inherited properties were silently skipped.
    /// </summary>
    [Fact]
    public void IgnoreNonExisting_ShouldMapInheritedProperties()
    {
        // Arrange — DTO has Id and CreatedAt from LimBaseDto
        var dto = new LimProductDto
        {
            Id = 42,
            CreatedAt = new DateTime(2025, 6, 15),
            Name = "Widget",
            Price = 9.99m,
            DisplayLabel = "Should not map to entity"
        };

        // Act
        var entity = _mapper.Map<LimProductDto, LimProductEntity>(dto);

        // Assert — inherited properties MUST be mapped
        entity.Id.Should().Be(42, "Id is inherited from LimBaseEntity and must be mapped");
        entity.CreatedAt.Should().Be(new DateTime(2025, 6, 15), "CreatedAt is inherited and must be mapped");

        // Derived properties
        entity.Name.Should().Be("Widget");
        entity.Price.Should().Be(9.99m);

        // InternalCode has no matching source — should keep default
        entity.InternalCode.Should().Be("");
    }

    /// <summary>
    /// Verifies that ReverseMap propagates IgnoreNonExisting.
    /// Without the fix, the reverse direction would emit ZMAP001 diagnostics
    /// for non-matching properties (InternalCode, DisplayLabel).
    /// The fact that this test COMPILES proves IgnoreNonExisting was propagated.
    /// </summary>
    [Fact]
    public void ReverseMap_ShouldPropagateIgnoreNonExisting_WithInheritedProperties()
    {
        // Arrange — entity has Id and CreatedAt from LimBaseEntity
        var entity = new LimProductEntity
        {
            Id = 7,
            CreatedAt = new DateTime(2024, 3, 10),
            Name = "Gadget",
            Price = 29.99m,
            InternalCode = "INT-001"
        };

        // Act — reverse mapping: Entity → DTO
        var dto = _mapper.Map<LimProductEntity, LimProductDto>(entity);

        // Assert — inherited properties must be mapped in reverse too
        dto.Id.Should().Be(7);
        dto.CreatedAt.Should().Be(new DateTime(2024, 3, 10));

        // Derived properties
        dto.Name.Should().Be("Gadget");
        dto.Price.Should().Be(29.99m);

        // DisplayLabel has no matching source — should keep default
        dto.DisplayLabel.Should().Be("default");
    }

    /// <summary>
    /// Verifies that extension methods also work for IgnoreNonExisting with inheritance.
    /// </summary>
    [Fact]
    public void ExtensionMethod_ShouldMapInheritedProperties_WithIgnoreNonExisting()
    {
        // Arrange
        var dto = new LimProductDto
        {
            Id = 100,
            CreatedAt = new DateTime(2025, 1, 1),
            Name = "ExtTest",
            Price = 5.50m
        };

        // Act — use extension method instead of IMapper
        var entity = dto.ToLimProductEntity();

        // Assert — inherited properties must be mapped
        entity.Id.Should().Be(100);
        entity.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        entity.Name.Should().Be("ExtTest");
        entity.Price.Should().Be(5.50m);
    }

    // ---- Limitation #3: Nullable in object initializer (required properties) ----

    /// <summary>
    /// Verifies that nullable value types are correctly mapped to required non-nullable
    /// destination properties when values are present.
    /// The 'required' keyword forces the object initializer code path in the generator.
    /// </summary>
    [Fact]
    public void NullableToRequired_ShouldMapValuesCorrectly()
    {
        // Arrange — all nullable properties have values
        var source = new NullableInitSource
        {
            Id = 1,
            EventDate = new DateTime(2025, 12, 25),
            Quantity = 10,
            Amount = 99.99m,
            Name = "Test Event"
        };

        // Act
        var dest = _mapper.Map<NullableInitSource, RequiredInitDest>(source);

        // Assert
        dest.Id.Should().Be(1);
        dest.EventDate.Should().Be(new DateTime(2025, 12, 25));
        dest.Quantity.Should().Be(10);
        dest.Amount.Should().Be(99.99m);
        dest.Name.Should().Be("Test Event");
    }

    /// <summary>
    /// Verifies that null nullable values map to default(T) for required properties.
    /// Before the fix, this would produce CS0266 at compile time because
    /// the object initializer path did not apply ?? default.
    /// </summary>
    [Fact]
    public void NullableToRequired_NullValues_ShouldUseDefaults()
    {
        // Arrange — nullable properties are null
        var source = new NullableInitSource
        {
            Id = 2,
            EventDate = null,
            Quantity = null,
            Amount = null,
            Name = "Null Test"
        };

        // Act
        var dest = _mapper.Map<NullableInitSource, RequiredInitDest>(source);

        // Assert — null values should produce default(T)
        dest.Id.Should().Be(2);
        dest.EventDate.Should().Be(default(DateTime), "null DateTime? should map to DateTime.MinValue");
        dest.Quantity.Should().Be(0, "null int? should map to 0");
        dest.Amount.Should().Be(0m, "null decimal? should map to 0m");
        dest.Name.Should().Be("Null Test");
    }

    /// <summary>
    /// Verifies the extension method path also handles nullable-to-required correctly.
    /// Extension methods have a separate object initializer code path in the generator.
    /// </summary>
    [Fact]
    public void ExtensionMethod_NullableToRequired_ShouldWork()
    {
        // Arrange
        var source = new NullableInitSource
        {
            Id = 3,
            EventDate = new DateTime(2026, 1, 1),
            Quantity = null,
            Amount = 50.00m,
            Name = "Extension Test"
        };

        // Act — use extension method
        var dest = source.ToRequiredInitDest();

        // Assert
        dest.Id.Should().Be(3);
        dest.EventDate.Should().Be(new DateTime(2026, 1, 1));
        dest.Quantity.Should().Be(0, "null int? should map to 0 via extension method");
        dest.Amount.Should().Be(50.00m);
        dest.Name.Should().Be("Extension Test");
    }
}
