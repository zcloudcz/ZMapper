using System.Diagnostics;
using ZMapper;
using ZMapper.Example;

Console.WriteLine("=== ZMapper Example ===\n");

// Create the unified mapper from all profiles (no DI needed)
// In a real app, you'd use: builder.Services.AddZMapper();
IMapper mapper = Mapper.Create();

// Example 1: Simple mapping
Console.WriteLine("Example 1: Simple User Mapping (via Profile)");
Console.WriteLine("---------------------------------------------");

var userDto = new UserDto
{
    Id = 1,
    Username = "john.doe",
    Email = "john@example.com",
    FirstName = "John",
    LastName = "Doe",
    CreatedAt = DateTime.Now.AddDays(-30),
    IsActive = true
};

var user = mapper.Map<UserDto, User>(userDto);

Console.WriteLine($"Source: {userDto.Id}, {userDto.Username}, {userDto.Email}");
Console.WriteLine($"Mapped: {user.Id}, {user.UserName}, {user.EmailAddress}");
Console.WriteLine($"Active: {userDto.IsActive} -> {user.Active}");
Console.WriteLine();

// Example 2: Address mapping
Console.WriteLine("Example 2: Address Mapping (via Profile)");
Console.WriteLine("-----------------------------------------");

var addressDto = new AddressDto
{
    Street = "123 Main St",
    City = "Prague",
    PostalCode = "110 00",
    Country = "Czech Republic"
};

var address = mapper.Map<AddressDto, Address>(addressDto);

Console.WriteLine($"Source: {addressDto.Street}, {addressDto.City}");
Console.WriteLine($"Mapped: {address.StreetAddress}, {address.City}");
Console.WriteLine($"Country: {addressDto.Country} -> {address.CountryName}");
Console.WriteLine();

// Example 3: Performance test
Console.WriteLine("Example 3: Performance Test");
Console.WriteLine("---------------------------");

const int iterations = 100_000;

var stopwatch = Stopwatch.StartNew();
for (int i = 0; i < iterations; i++)
{
    var testUser = mapper.Map<UserDto, User>(userDto);
}
stopwatch.Stop();

Console.WriteLine($"Mapped {iterations:N0} objects in {stopwatch.ElapsedMilliseconds:N0} ms");
Console.WriteLine($"Average: {(stopwatch.ElapsedMilliseconds * 1_000_000.0 / iterations):F2} ns per mapping");
Console.WriteLine($"Throughput: {(iterations / stopwatch.Elapsed.TotalSeconds):N0} mappings/sec");
Console.WriteLine();

// Example 4: Array mapping with Span
Console.WriteLine("Example 4: Array Mapping with Span");
Console.WriteLine("-----------------------------------");

var userDtos = new[]
{
    new UserDto { Id = 1, Username = "user1", Email = "user1@test.com", CreatedAt = DateTime.Now, IsActive = true },
    new UserDto { Id = 2, Username = "user2", Email = "user2@test.com", CreatedAt = DateTime.Now, IsActive = false },
    new UserDto { Id = 3, Username = "user3", Email = "user3@test.com", CreatedAt = DateTime.Now, IsActive = true },
};

stopwatch.Restart();
var users = mapper.MapArray<UserDto, User>(userDtos.AsSpan());
stopwatch.Stop();

var microseconds = stopwatch.Elapsed.TotalMicroseconds;
Console.WriteLine($"Mapped {userDtos.Length} users in {microseconds:F2} us");
Console.WriteLine($"Results: {string.Join(", ", users.Select(u => u.UserName))}");
Console.WriteLine();

// Example 5: IEnumerable mapping (new feature)
Console.WriteLine("Example 5: IEnumerable Mapping");
Console.WriteLine("-------------------------------");

IEnumerable<UserDto> filteredDtos = userDtos.Where(u => u.IsActive);
var activeUsers = mapper.MapList<UserDto, User>(filteredDtos);

Console.WriteLine($"Filtered {userDtos.Length} DTOs -> {activeUsers.Count} active users");
Console.WriteLine($"Active users: {string.Join(", ", activeUsers.Select(u => u.UserName))}");
Console.WriteLine();

Console.WriteLine("=== All examples completed successfully! ===");
