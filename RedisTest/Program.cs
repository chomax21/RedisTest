using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationContext>(options =>
{
    options.UseSqlite("Data Sourse=userdata.db");
});
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost";
    options.InstanceName = "local";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapPost("/user/{id}", async (int id, UserService userService) =>
{
    User? user = await userService.GetUser(id);
    if (user != null) return $"User {user.Name}  Id={user.Id}  Age={user.Age}";
    return "User not found";
});

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateTime.Now.AddDays(index),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

public class User
{
    public int Id { get; set; }
    public string? Name { get; set; } = "default";
    public int Age { get; set; }
}
public class ApplicationContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options) =>
        Database.EnsureCreated();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Age = 23, Name = "Kirrik" },
            new User { Id = 2, Age = 32, Name = "Maximka" });
    }
}

public class UserService
{
    private ApplicationContext _db;
    private IDistributedCache _cache;

    public UserService(ApplicationContext context, IDistributedCache distributedCache)
    {
        _db = context;
        _cache = distributedCache;
    }

    public async Task<User?> GetUser(int id)
    {
        User? user = null;
        var userString = await _cache.GetStringAsync(id.ToString());
        if (userString != null)
            user = JsonSerializer.Deserialize<User>(userString);
        if (user == null)
        {
            user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"User {user.Name} извлечен из базы!");
                Console.ResetColor();
                userString = JsonSerializer.Serialize<User>(user);
                await _cache.SetStringAsync(user.Id.ToString(), userString, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                });
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"User {user.Name} извлечен из кэша!");
            Console.ResetColor();
        }
        return user;
    }
}


internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}