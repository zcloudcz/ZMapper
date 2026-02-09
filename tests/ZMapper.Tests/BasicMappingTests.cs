using FluentAssertions;
using Xunit;

namespace ZMapper.Tests;

// Test DTOs for BasicMappingTests
public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class PersonEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
}

public partial class TestMapperConfig
{
    public static IMapper CreateTestMapper()
    {
        var config = new MapperConfiguration();

        // Convention-based mapping (matching property names)
        config.CreateMap<PersonDto, PersonEntity>();

        // Explicit property mapping
        config.CreateMap<UserDto, User>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username))
            .ForMember(dest => dest.EmailAddress, opt => opt.MapFrom(src => src.Email));

        return CreateGeneratedMapper();
    }
}

/// <summary>
/// Tests for basic mapping scenarios - convention-based and explicit property mapping
/// </summary>
public class BasicMappingTests
{

    private readonly IMapper _mapper;

    public BasicMappingTests()
    {
        _mapper = TestMapperConfig.CreateTestMapper();
    }

    [Fact]
    public void ConventionBasedMapping_ShouldMapMatchingProperties()
    {
        // Arrange
        var dto = new PersonDto
        {
            Id = 123,
            Name = "John Doe",
            Age = 30
        };

        // Act
        var entity = _mapper.Map<PersonDto, PersonEntity>(dto);

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(123);
        entity.Name.Should().Be("John Doe");
        entity.Age.Should().Be(30);
    }

    [Fact]
    public void ExplicitMapping_ShouldMapConfiguredProperties()
    {
        // Arrange
        var dto = new UserDto
        {
            Id = 42,
            Username = "johndoe",
            Email = "john@example.com"
        };

        // Act
        var user = _mapper.Map<UserDto, User>(dto);

        // Assert
        user.Should().NotBeNull();
        user.UserId.Should().Be(42);
        user.UserName.Should().Be("johndoe");
        user.EmailAddress.Should().Be("john@example.com");
    }

    [Fact]
    public void Mapping_WithDefaultValues_ShouldMapCorrectly()
    {
        // Arrange
        var dto = new PersonDto(); // All default values

        // Act
        var entity = _mapper.Map<PersonDto, PersonEntity>(dto);

        // Assert
        entity.Should().NotBeNull();
        entity.Id.Should().Be(0);
        entity.Name.Should().Be(string.Empty);
        entity.Age.Should().Be(0);
    }
}
