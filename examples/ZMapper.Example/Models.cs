namespace ZMapper.Example;

// Source models
public class UserDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddressDto
{
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? PostalCode { get; set; }
    public required string Country { get; set; }
}

// Destination models
public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public string? FullName { get; set; }
    public DateTime RegisteredDate { get; set; }
    public bool Active { get; set; }
}

public class Address
{
    public string StreetAddress { get; set; } = "";
    public string City { get; set; } = "";
    public string? Zip { get; set; }
    public string CountryName { get; set; } = "";
}

// Complex nested model
public class CustomerDto
{
    public required string CustomerName { get; set; }
    public required string Email { get; set; }
    public AddressDto? ShippingAddress { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class Customer
{
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public Address? Address { get; set; }
    public List<string> Labels { get; set; } = new();
}
