using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for ReverseMap propagation of new flags
// ============================================================================

/// <summary>Source with flags that should PreCondition-filter on reverse too.</summary>
public class RmPartialSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>Destination that will be reversed back into source.</summary>
public class RmPartialDest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

/// <summary>Source for MemberList-with-ReverseMap test. Has extra property.</summary>
public class RmMemberListSource
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Secret { get; set; }  // present on source only
}

public class RmMemberListDest
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Source with custom property name that gets swapped on ReverseMap.</summary>
public class RmRenamedSource
{
    public int SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class RmRenamedDest
{
    public int DestId { get; set; }
    public string Title { get; set; } = string.Empty;
}

// ============================================================================
// Mapper config exercising ReverseMap + new features
// ============================================================================
public partial class ReverseMapPropagationConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // PreCondition propagated to reverse — context controls Detail on both directions
        config.CreateMap<RmPartialSource, RmPartialDest>()
            .ForMember(d => d.Detail,
                opt => opt.PreCondition((src, ctx) => ctx.GetOrDefault<bool>("WithDetail")))
            .ReverseMap();

        // MemberList.None + ReverseMap — no ZMAP001/ZMAP003 warnings either direction
        config.CreateMap<RmMemberListSource, RmMemberListDest>(MemberList.None)
            .ReverseMap();

        // ReverseMap with ForMember — confirms normal inversion still works
        config.CreateMap<RmRenamedSource, RmRenamedDest>()
            .ForMember(d => d.DestId, opt => opt.MapFrom(s => s.SourceId))
            .ReverseMap();

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================
public class ReverseMapPropagationTests
{
    private readonly IMapper _mapper;

    public ReverseMapPropagationTests()
    {
        _mapper = ReverseMapPropagationConfig.CreateMapper();
    }

    // --- PreCondition propagation ---

    [Fact]
    public void ReverseMap_PreCondition_Forward_RespectsContext()
    {
        var source = new RmPartialSource { Id = 1, Name = "A", Detail = "secret" };

        var ctxOff = new MappingContext();
        var resultOff = _mapper.Map<RmPartialSource, RmPartialDest>(source, ctxOff);
        resultOff.Detail.Should().BeNull("WithDetail is false by default");

        var ctxOn = new MappingContext();
        ctxOn["WithDetail"] = true;
        var resultOn = _mapper.Map<RmPartialSource, RmPartialDest>(source, ctxOn);
        resultOn.Detail.Should().Be("secret");
    }

    [Fact]
    public void ReverseMap_PreCondition_Reverse_RespectsContext()
    {
        // The reverse mapping (RmPartialDest -> RmPartialSource) should also honor PreCondition
        var dest = new RmPartialDest { Id = 2, Name = "B", Detail = "info" };

        var ctxOff = new MappingContext();
        var resultOff = _mapper.Map<RmPartialDest, RmPartialSource>(dest, ctxOff);
        resultOff.Id.Should().Be(2);
        resultOff.Name.Should().Be("B");
        resultOff.Detail.Should().BeNull("PreCondition propagated to reverse — blocks Detail");

        var ctxOn = new MappingContext();
        ctxOn["WithDetail"] = true;
        var resultOn = _mapper.Map<RmPartialDest, RmPartialSource>(dest, ctxOn);
        resultOn.Detail.Should().Be("info");
    }

    // --- MemberList propagation ---

    [Fact]
    public void ReverseMap_MemberListNone_NoWarnings_BothDirections()
    {
        // Both directions work — the fact that the build succeeded already proves
        // MemberList.None was propagated (otherwise ZMAP001 would fire for reverse).
        var source = new RmMemberListSource { Id = 5, Label = "X", Secret = "hidden" };
        var dest = source.ToRmMemberListDest();
        dest.Id.Should().Be(5);
        dest.Label.Should().Be("X");

        var reversed = dest.ToRmMemberListSource();
        reversed.Id.Should().Be(5);
        reversed.Label.Should().Be("X");
        reversed.Secret.Should().BeNull("source-only property, not mapped");
    }

    // --- Standard ReverseMap still works after changes ---

    [Fact]
    public void ReverseMap_NormalInversion_StillWorks()
    {
        var source = new RmRenamedSource { SourceId = 99, Title = "Hello" };
        var dest = _mapper.Map<RmRenamedSource, RmRenamedDest>(source);
        dest.DestId.Should().Be(99);
        dest.Title.Should().Be("Hello");

        // Reverse direction
        var reversed = _mapper.Map<RmRenamedDest, RmRenamedSource>(dest);
        reversed.SourceId.Should().Be(99);
        reversed.Title.Should().Be("Hello");
    }
}
