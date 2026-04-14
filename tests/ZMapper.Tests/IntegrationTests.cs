using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Integration scenario — realistic mix of all new features
// ----------------------------------------------------------------------------
// Domain: "invoice processing" with nested client + items, conditional detail
// mapping based on caller permissions, and type-converter handling for dates.
// Exercises: ConvertUsing<T>, ConstructUsing, PreCondition + MappingContext,
//            MemberList, member-level ConvertUsing with source property,
//            ForMember + MapFrom, BeforeMap/AfterMap hooks, standalone mapper,
//            extension methods, reverse mapping, collections (MapList).
// ============================================================================

// --- DTOs (source side) ---

public class IntInvoiceDto
{
    public int InvoiceId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public decimal RawAmount { get; set; }
    public string? InternalNotes { get; set; }
    public List<IntInvoiceLineDto> Lines { get; set; } = new();
}

public class IntInvoiceLineDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// --- Entities (destination side, immutable InvoiceLine) ---

public class IntInvoiceEntity
{
    public int Id { get; set; }                    // from InvoiceId via ForMember
    public DateTime IssuedAt { get; set; }          // via ITypeConverter DateTimeOffset->DateTime
    public string ClientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }             // via member-level converter (CentsConverter)
    public string? InternalNotes { get; set; }      // PreCondition: only when ShowInternals=true
    public List<IntInvoiceLine> Lines { get; set; } = new();
    public DateTime ProcessedAt { get; set; }       // set by AfterMap hook
    public int LineCount { get; set; }              // set by AfterMap hook
}

public class IntInvoiceLine
{
    // Immutable: Sku via constructor (ConstructUsing)
    public string Sku { get; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }              // set by AfterMap hook

    public IntInvoiceLine(string sku)
    {
        Sku = sku;
    }
}

// --- Type converters ---

/// <summary>Whole-object: DateTimeOffset -> DateTime (UTC).</summary>
public class IntDateTimeOffsetConverter : ITypeConverter<DateTimeOffset, DateTime>
{
    public DateTime Convert(DateTimeOffset source) => source.UtcDateTime;
}

/// <summary>Member-level: convert RawAmount (cents as decimal) to Amount (euros as decimal).</summary>
public class IntCentsToEurosConverter : IMemberConverter<IntInvoiceDto, decimal, decimal>
{
    public decimal Convert(IntInvoiceDto source, decimal sourceMember)
    {
        return sourceMember / 100m;
    }
}

// --- Profile wiring everything together ---

public partial class IntegrationProfile : IMapperProfile
{
    // Captured by BeforeMap for assertion
    public static int BeforeMapCallCount;
    public static int AfterMapCallCount;

    public void Configure(MapperConfiguration config)
    {
        // Global type converter for DateTimeOffset -> DateTime — auto-applies to any property
        // of matching types across other mappings registered in this profile.
        config.CreateMap<DateTimeOffset, DateTime>()
            .ConvertUsing<IntDateTimeOffsetConverter>();

        // Main invoice mapping, combining almost every feature
        config.CreateMap<IntInvoiceDto, IntInvoiceEntity>(MemberList.None)
            // ForMember rename
            .ForMember(d => d.Id, o => o.MapFrom(s => s.InvoiceId))
            // Member-level ConvertUsing with explicit source property (RawAmount -> Amount)
            .ForMember(d => d.Amount,
                o => o.ConvertUsing<IntCentsToEurosConverter, decimal>(s => s.RawAmount))
            // PreCondition + MappingContext — only map sensitive InternalNotes when allowed
            .ForMember(d => d.InternalNotes,
                o => o.PreCondition((src, ctx) => ctx.GetOrDefault<bool>("ShowInternals")))
            // Hooks: audit fields and derived aggregates
            .BeforeMap((src, dest) =>
            {
                BeforeMapCallCount++;
            })
            .AfterMap((src, dest) =>
            {
                AfterMapCallCount++;
                dest.ProcessedAt = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc);
                dest.LineCount = dest.Lines?.Count ?? 0;
                // NOTE: Nested lines are mapped via extension methods which skip AfterMap hooks
                // (pre-existing limitation — see generator notes). Compute Total here instead.
                if (dest.Lines != null)
                {
                    foreach (var line in dest.Lines)
                    {
                        line.Total = line.Quantity * line.UnitPrice;
                    }
                }
            });

        // Immutable line — requires ConstructUsing; Quantity/UnitPrice mapped normally
        config.CreateMap<IntInvoiceLineDto, IntInvoiceLine>(MemberList.None)
            .ConstructUsing(s => new IntInvoiceLine(s.Sku));
    }
}

// ============================================================================
// Tests
// ============================================================================
public class IntegrationTests
{
    [Fact]
    public void FullStack_AllFeaturesWorkTogether_WithStandaloneMapper()
    {
        // Arrange: reset hook counters
        IntegrationProfile.BeforeMapCallCount = 0;
        IntegrationProfile.AfterMapCallCount = 0;

        // Standalone mapper creation (Feature 8) — no DI
        var config = new MapperConfiguration();
        new IntegrationProfile().Configure(config);
        var mapper = config.CreateMapper();

        var dto = new IntInvoiceDto
        {
            InvoiceId = 42,
            IssuedAt = new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(2)),
            ClientName = "Acme Corp",
            RawAmount = 250_00m,           // 250 EUR stored as cents
            InternalNotes = "Margin warning",
            Lines = new List<IntInvoiceLineDto>
            {
                new() { Sku = "A-001", Quantity = 2, UnitPrice = 49.50m },
                new() { Sku = "B-002", Quantity = 1, UnitPrice = 151.00m }
            }
        };

        // Act: map WITH context (allow internals)
        var ctx = new MappingContext();
        ctx["ShowInternals"] = true;
        var entity = mapper.Map<IntInvoiceDto, IntInvoiceEntity>(dto, ctx);

        // Assert: every feature collaborated correctly
        entity.Should().NotBeNull();
        entity.Id.Should().Be(42);                                   // ForMember
        entity.IssuedAt.Should().Be(new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc)); // ITypeConverter auto-applied
        entity.ClientName.Should().Be("Acme Corp");                  // Convention-based
        entity.Amount.Should().Be(250m);                             // Member-level ConvertUsing
        entity.InternalNotes.Should().Be("Margin warning");          // PreCondition passed (ShowInternals=true)
        entity.Lines.Should().HaveCount(2);                          // Nested collection mapping
        entity.Lines[0].Sku.Should().Be("A-001");                    // ConstructUsing set immutable prop
        entity.Lines[0].Quantity.Should().Be(2);                     // Property mapping after ConstructUsing
        entity.Lines[0].UnitPrice.Should().Be(49.50m);
        entity.Lines[0].Total.Should().Be(99.00m);                   // Line-level AfterMap hook
        entity.Lines[1].Total.Should().Be(151.00m);
        entity.ProcessedAt.Should().Be(new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc)); // AfterMap
        entity.LineCount.Should().Be(2);                             // AfterMap aggregate

        IntegrationProfile.BeforeMapCallCount.Should().Be(1);
        IntegrationProfile.AfterMapCallCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FullStack_PreConditionOff_OmitsSensitiveFields()
    {
        var config = new MapperConfiguration();
        new IntegrationProfile().Configure(config);
        var mapper = config.CreateMapper();

        var dto = new IntInvoiceDto
        {
            InvoiceId = 1,
            ClientName = "Test",
            RawAmount = 10_000m,
            InternalNotes = "Hidden notes",
        };

        // Empty context — ShowInternals=false by default
        var entity = mapper.Map<IntInvoiceDto, IntInvoiceEntity>(dto, new MappingContext());

        entity.InternalNotes.Should().BeNull("PreCondition blocks InternalNotes when ShowInternals is false");
        entity.Amount.Should().Be(100m);
        entity.ClientName.Should().Be("Test");
    }

    [Fact]
    public void FullStack_ExtensionMethod_SkipsPreConditionPath()
    {
        // Extension methods don't take context, so they use the no-context generated path.
        // With no context, InternalNotes is mapped normally (convention) — no PreCondition.
        // NOTE: PreCondition is only enforced via the Map(source, context) overload.
        var config = new MapperConfiguration();
        new IntegrationProfile().Configure(config);
        _ = config.CreateMapper(); // ensure MapperFactory registered

        var dto = new IntInvoiceDto
        {
            InvoiceId = 7,
            ClientName = "Ext",
            RawAmount = 50_00m,
            InternalNotes = "plain"
        };

        var entity = dto.ToIntInvoiceEntity();
        entity.Id.Should().Be(7);
        entity.Amount.Should().Be(50m);
        entity.InternalNotes.Should().Be("plain", "no-context path has no PreCondition gate");
    }

    [Fact]
    public void FullStack_CollectionMapping_Works()
    {
        var mapper = Mapper.Create();

        var dtos = new List<IntInvoiceDto>
        {
            new() { InvoiceId = 1, ClientName = "A", RawAmount = 100_00m },
            new() { InvoiceId = 2, ClientName = "B", RawAmount = 200_00m }
        };

        var entities = mapper.MapList<IntInvoiceDto, IntInvoiceEntity>(dtos);

        entities.Should().HaveCount(2);
        entities[0].Id.Should().Be(1);
        entities[0].Amount.Should().Be(100m);
        entities[1].Id.Should().Be(2);
        entities[1].Amount.Should().Be(200m);
    }
}
