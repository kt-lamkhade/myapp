using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

// Create the table on startup; retry while the database pod is still starting.
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDb>().Database.EnsureCreated();
        break;
    }
    catch when (attempt < 30)
    {
        Console.WriteLine($"Database not ready (attempt {attempt}), retrying...");
        Thread.Sleep(2000);
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok("ok"));

app.MapGet("/api/students", async (AppDb db) =>
    await db.Students.OrderByDescending(s => s.Id).ToListAsync());

app.MapPost("/api/students", async (Student s, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(s.Name) || string.IsNullOrWhiteSpace(s.Gender) || s.Age is < 1 or > 120)
        return Results.BadRequest("Name, gender and a valid age (1-120) are required.");
    s.Id = 0;
    db.Students.Add(s);
    await db.SaveChangesAsync();
    return Results.Created($"/api/students/{s.Id}", s);
});

app.Run();

public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Gender { get; set; } = "";
    public int Age { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
}
