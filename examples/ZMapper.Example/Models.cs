namespace ZMapper.Example;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public abstract class BaseModel
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Source models
public class UserDto : BaseModel
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddressDto : BaseModel
{
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? PostalCode { get; set; }
    public required string Country { get; set; }
}

// Destination models
public class User : BaseEntity
{
    public string UserName { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public string? FullName { get; set; }
    public DateTime RegisteredDate { get; set; }
    public bool Active { get; set; }
}

public class Address : BaseEntity
{
    public string StreetAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string? Zip { get; set; }
    public string CountryName { get; set; } = "";
}

// Complex nested model
public class CustomerDto : BaseModel
{
    public required string CustomerName { get; set; }
    public required string Email { get; set; }
    public AddressDto? ShippingAddress { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class Customer : BaseEntity
{
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public Address? Address { get; set; }
    public List<string> Labels { get; set; } = new();
}
