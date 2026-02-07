using FluentAssertions;
using ZMapper.Abstractions;
using Xunit;

namespace ZMapper.Tests;

// === Base classes for inheritance tests ===

/// <summary>
/// Abstract base entity with common properties (Id, CreatedAt, UpdatedAt).
/// Simulates a typical EF Core base entity pattern used in production apps.
/// </summary>
public abstract class InhBaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Abstract base DTO mirroring the base entity properties.
/// </summary>
public abstract class InhBaseDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// === Single-level inheritance ===

/// <summary>
/// A concrete entity inheriting from InhBaseEntity.
/// Only declares its own properties; Id/CreatedAt/UpdatedAt come from the base class.
/// </summary>
public class InhProductEntity : InhBaseEntity
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

/// <summary>
/// A concrete DTO inheriting from InhBaseDto.
/// </summary>
public class InhProductDto : InhBaseDto
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

// === Multi-level inheritance (3 levels) ===

/// <summary>
/// Middle-level class adding a property between base and leaf.
/// Tests 3-level inheritance: InhBaseEntity -> InhAuditableEntity -> InhAuditedProductEntity.
/// </summary>
public class InhAuditableEntity : InhBaseEntity
{
    public string ModifiedBy { get; set; } = "";
}

public class InhAuditedProductEntity : InhAuditableEntity
{
    public string ProductName { get; set; } = "";
    public decimal ProductPrice { get; set; }
}

public class InhAuditableDto : InhBaseDto
{
    public string ModifiedBy { get; set; } = "";
}

public class InhAuditedProductDto : InhAuditableDto
{
    public string ProductName { get; set; } = "";
    public decimal ProductPrice { get; set; }
}

// === Property hiding (new keyword) ===

/// <summary>
/// Derived class that re-declares (hides) CreatedAt with 'new'.
/// The derived version should win during mapping.
/// </summary>
public class InhOverridingEntity : InhBaseEntity
{
    public new DateTime CreatedAt { get; set; }
    public string Label { get; set; } = "";
}

public class InhOverridingDto : InhBaseDto
{
    public new DateTime CreatedAt { get; set; }
    public string Label { get; set; } = "";
}

// === Nullable value type mismatch test (Issue #6) ===

/// <summary>
/// Source with nullable value types (DateTime?, int?).
/// Simulates an entity where some fields are optional in the database.
/// </summary>
public class NullableSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime? IssueDate { get; set; }
    public int? Quantity { get; set; }
    public decimal? Price { get; set; }
}

/// <summary>
/// Destination with non-nullable value types.
/// The generated code must handle DateTime? -> DateTime without CS0266 error.
/// </summary>
public class NonNullableDestEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime IssueDate { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

// === Mapper configuration using the CreateMap pattern ===

/// <summary>
/// Partial class that registers all inheritance-related mappings.
/// Uses the standard pattern: static CreateMapper() method with MapperConfiguration.
/// The source generator will emit the mapping code for these at compile time.
/// </summary>
public partial class InheritanceMappingConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Basic single-level inheritance
        config.CreateMap<InhProductDto, InhProductEntity>();
        config.CreateMap<InhProductEntity, InhProductDto>();

        // Multi-level (3-level) inheritance
        config.CreateMap<InhAuditedProductDto, InhAuditedProductEntity>();
        config.CreateMap<InhAuditedProductEntity, InhAuditedProductDto>();

        // Property hiding with 'new' keyword
        config.CreateMap<InhOverridingDto, InhOverridingEntity>();

        // Nullable value type -> non-nullable value type (Issue #6: CS0266 fix)
        config.CreateMap<NullableSourceDto, NonNullableDestEntity>();

        return CreateGeneratedMapper();
    }
}

// === Tests ===

public class InheritanceMappingTests
{
    private readonly IMapper _mapper = InheritanceMappingConfig.CreateMapper();

    /// <summary>
    /// Verifies that properties inherited from a base class (Id, CreatedAt, UpdatedAt)
    /// are correctly mapped alongside the derived class's own properties.
    /// This was the original production bug: base class properties were silently ignored.
    /// </summary>
    [Fact]
    public void Should_Map_BaseClass_Properties()
    {
        var dto = new InhProductDto
        {
            Id = 42,
            Name = "Widget",
            Price = 9.99m,
            CreatedAt = new DateTime(2025, 1, 1),
            UpdatedAt = new DateTime(2025, 6, 15)
        };

        var entity = _mapper.Map<InhProductDto, InhProductEntity>(dto);

        // Base class properties must be mapped
        entity.Id.Should().Be(42, "Id is inherited from InhBaseEntity and must be mapped");
        entity.CreatedAt.Should().Be(new DateTime(2025, 1, 1), "CreatedAt is inherited and must be mapped");
        entity.UpdatedAt.Should().Be(new DateTime(2025, 6, 15), "UpdatedAt is inherited and must be mapped");

        // Derived class properties
        entity.Name.Should().Be("Widget");
        entity.Price.Should().Be(9.99m);
    }

    /// <summary>
    /// Verifies reverse mapping (entity -> DTO) also includes base class properties.
    /// </summary>
    [Fact]
    public void Should_Map_BaseClass_Properties_In_Reverse()
    {
        var entity = new InhProductEntity
        {
            Id = 7,
            Name = "Gadget",
            Price = 29.99m,
            CreatedAt = new DateTime(2024, 3, 10),
            UpdatedAt = new DateTime(2024, 12, 25)
        };

        var dto = _mapper.Map<InhProductEntity, InhProductDto>(entity);

        dto.Id.Should().Be(7);
        dto.CreatedAt.Should().Be(new DateTime(2024, 3, 10));
        dto.UpdatedAt.Should().Be(new DateTime(2024, 12, 25));
        dto.Name.Should().Be("Gadget");
        dto.Price.Should().Be(29.99m);
    }

    /// <summary>
    /// Tests 3-level inheritance: InhBaseDto -> InhAuditableDto -> InhAuditedProductDto.
    /// Properties from ALL levels must be mapped correctly.
    /// </summary>
    [Fact]
    public void Should_Map_MultiLevel_Inheritance()
    {
        var dto = new InhAuditedProductDto
        {
            Id = 100,                                       // from InhBaseDto (level 1)
            CreatedAt = new DateTime(2025, 1, 1),           // from InhBaseDto (level 1)
            UpdatedAt = new DateTime(2025, 6, 1),           // from InhBaseDto (level 1)
            ModifiedBy = "admin",                           // from InhAuditableDto (level 2)
            ProductName = "Premium Widget",                 // from InhAuditedProductDto (level 3)
            ProductPrice = 99.99m                           // from InhAuditedProductDto (level 3)
        };

        var entity = _mapper.Map<InhAuditedProductDto, InhAuditedProductEntity>(dto);

        // Level 1 - base class
        entity.Id.Should().Be(100);
        entity.CreatedAt.Should().Be(new DateTime(2025, 1, 1));
        entity.UpdatedAt.Should().Be(new DateTime(2025, 6, 1));

        // Level 2 - middle class
        entity.ModifiedBy.Should().Be("admin");

        // Level 3 - leaf class
        entity.ProductName.Should().Be("Premium Widget");
        entity.ProductPrice.Should().Be(99.99m);
    }

    /// <summary>
    /// When a derived class re-declares a property with 'new', the derived version should be used.
    /// </summary>
    [Fact]
    public void Should_Use_Derived_Property_When_Hidden_With_New()
    {
        var dto = new InhOverridingDto
        {
            Id = 5,
            CreatedAt = new DateTime(2025, 7, 4),  // This is the 'new' property on InhOverridingDto
            UpdatedAt = new DateTime(2025, 8, 1),
            Label = "Override Test"
        };

        var entity = _mapper.Map<InhOverridingDto, InhOverridingEntity>(dto);

        entity.Id.Should().Be(5);
        entity.CreatedAt.Should().Be(new DateTime(2025, 7, 4));
        entity.Label.Should().Be("Override Test");
    }

    /// <summary>
    /// Verifies that default values (0, default DateTime) are correctly mapped
    /// for inherited properties when they are not explicitly set.
    /// </summary>
    [Fact]
    public void Should_Map_Default_Values_For_Inherited_Properties()
    {
        var dto = new InhProductDto
        {
            Name = "Default Test",
            Price = 1.00m
            // Id, CreatedAt, UpdatedAt are left at default values
        };

        var entity = _mapper.Map<InhProductDto, InhProductEntity>(dto);

        entity.Id.Should().Be(0);
        entity.CreatedAt.Should().Be(default(DateTime));
        entity.UpdatedAt.Should().Be(default(DateTime));
        entity.Name.Should().Be("Default Test");
    }

    /// <summary>
    /// Issue #6: When source has nullable value types (DateTime?, int?) and destination
    /// has non-nullable value types (DateTime, int), the generated code must use ?? default
    /// to avoid CS0266 compile error. This test verifies values are mapped when present.
    /// </summary>
    [Fact]
    public void Should_Map_Nullable_ValueType_To_NonNullable_With_Values()
    {
        var dto = new NullableSourceDto
        {
            Id = 1,
            Name = "Invoice",
            IssueDate = new DateTime(2025, 3, 15),
            Quantity = 10,
            Price = 99.99m
        };

        var entity = _mapper.Map<NullableSourceDto, NonNullableDestEntity>(dto);

        entity.Id.Should().Be(1);
        entity.Name.Should().Be("Invoice");
        entity.IssueDate.Should().Be(new DateTime(2025, 3, 15));
        entity.Quantity.Should().Be(10);
        entity.Price.Should().Be(99.99m);
    }

    /// <summary>
    /// When nullable source properties are null, destination should receive default values
    /// (DateTime.MinValue, 0, 0m) instead of throwing a compile or runtime error.
    /// </summary>
    [Fact]
    public void Should_Map_Null_ValueType_To_Default()
    {
        var dto = new NullableSourceDto
        {
            Id = 2,
            Name = "Empty Invoice",
            IssueDate = null,
            Quantity = null,
            Price = null
        };

        var entity = _mapper.Map<NullableSourceDto, NonNullableDestEntity>(dto);

        entity.Id.Should().Be(2);
        entity.Name.Should().Be("Empty Invoice");
        entity.IssueDate.Should().Be(default(DateTime));
        entity.Quantity.Should().Be(0);
        entity.Price.Should().Be(0m);
    }
}
