namespace ZMapper.Benchmarks;

// Source model
public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

// Destination model
public class Person
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string EmailAddress { get; set; } = "";
    public int Age { get; set; }
    public string Phone { get; set; } = "";
    public string AddressLine { get; set; } = "";
    public string City { get; set; } = "";
    public string CountryName { get; set; } = "";
    public DateTime RegistrationDate { get; set; }
}
