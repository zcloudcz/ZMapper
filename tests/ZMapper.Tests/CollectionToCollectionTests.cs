using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// ============================================================================
// Test models for collection-to-collection mapping (Bug #1 and #3 fixes)
// ============================================================================

/// <summary>Source element type for collection-to-collection mapping tests.</summary>
public class Cluster
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Destination element type — distinct name to verify correct element mapping.</summary>
public class ClusterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Second source element type — used to test multiple collection mappings
/// generating unique method names (Bug #3: Map_List_To_List duplicate).</summary>
public class Tag
{
    public int TagId { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Second destination element type.</summary>
public class TagDto
{
    public int TagId { get; set; }
    public string Label { get; set; } = string.Empty;
}

// ============================================================================
// Mapper configuration — registers List<A> -> List<B> mappings directly.
//
// Bug #1: Previously generated invalid code like destination.this[] = source.this[].
// Bug #3: Multiple CreateMap<List<X>, List<Y>>() generated duplicate method names
//         (all resolved to Map_List_To_List). Now generates Map_ListOfCluster_To_ListOfClusterDto, etc.
// ============================================================================
public partial class CollectionToCollectionMapperConfig
{
    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration();

        // Element mappings (required for per-element .ToClusterDto() / .ToTagDto() calls)
        config.CreateMap<Cluster, ClusterDto>();
        config.CreateMap<Tag, TagDto>();

        // Bug #1 + #3: Collection-to-collection mappings
        // These previously generated invalid code (this[] access) and duplicate method names.
        config.CreateMap<List<Cluster>, List<ClusterDto>>(MemberList.None);
        config.CreateMap<List<Tag>, List<TagDto>>(MemberList.None);

        return CreateGeneratedMapper();
    }
}

// ============================================================================
// Tests
// ============================================================================

/// <summary>
/// Tests for collection-to-collection mapping (CreateMap&lt;List&lt;A&gt;, List&lt;B&gt;&gt;()).
///
/// These tests verify that:
/// 1. The generator emits valid iteration code (not this[] property access) — Bug #1
/// 2. Multiple List-to-List mappings generate unique method names — Bug #3
/// 3. Elements are correctly mapped using their element-type mapping
/// 4. Empty collections are handled correctly
/// </summary>
public class CollectionToCollectionTests
{
    private readonly IMapper _mapper;

    public CollectionToCollectionTests()
    {
        _mapper = CollectionToCollectionMapperConfig.CreateMapper();
    }

    // --- Bug #1: Collection-to-collection should generate valid iteration code ---

    [Fact]
    public void ListToList_ShouldMapAllElements()
    {
        // Arrange
        var source = new List<Cluster>
        {
            new Cluster { Id = 1, Name = "Alpha" },
            new Cluster { Id = 2, Name = "Beta" },
            new Cluster { Id = 3, Name = "Gamma" }
        };

        // Act — previously generated invalid code with destination.this[]
        var result = _mapper.Map<List<Cluster>, List<ClusterDto>>(source);

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("Alpha");
        result[1].Id.Should().Be(2);
        result[1].Name.Should().Be("Beta");
        result[2].Id.Should().Be(3);
        result[2].Name.Should().Be("Gamma");
    }

    [Fact]
    public void ListToList_EmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        var source = new List<Cluster>();

        // Act
        var result = _mapper.Map<List<Cluster>, List<ClusterDto>>(source);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ListToList_SingleElement_ShouldWork()
    {
        // Arrange
        var source = new List<Cluster> { new Cluster { Id = 42, Name = "Single" } };

        // Act
        var result = _mapper.Map<List<Cluster>, List<ClusterDto>>(source);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(42);
        result[0].Name.Should().Be("Single");
    }

    // --- Bug #3: Multiple List<X> -> List<Y> mappings should not clash ---

    [Fact]
    public void MultipleListMappings_ShouldGenerateUniqueMethodNames()
    {
        // Arrange — two different collection-to-collection mappings coexist
        var clusters = new List<Cluster>
        {
            new Cluster { Id = 1, Name = "Cluster1" }
        };
        var tags = new List<Tag>
        {
            new Tag { TagId = 10, Label = "Tag1" }
        };

        // Act — both mappings must work without CS0111 (duplicate member) error
        var clusterResult = _mapper.Map<List<Cluster>, List<ClusterDto>>(clusters);
        var tagResult = _mapper.Map<List<Tag>, List<TagDto>>(tags);

        // Assert
        clusterResult.Should().HaveCount(1);
        clusterResult[0].Id.Should().Be(1);
        clusterResult[0].Name.Should().Be("Cluster1");

        tagResult.Should().HaveCount(1);
        tagResult[0].TagId.Should().Be(10);
        tagResult[0].Label.Should().Be("Tag1");
    }

    // --- Extension methods for collection-to-collection ---

    [Fact]
    public void ListToList_ExtensionMethod_ShouldWork()
    {
        // Arrange
        var source = new List<Cluster>
        {
            new Cluster { Id = 5, Name = "Ext" }
        };

        // Act — extension method should be named ToListOfClusterDto (not ToList)
        var result = source.ToListOfClusterDto();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(5);
        result[0].Name.Should().Be("Ext");
    }
}
