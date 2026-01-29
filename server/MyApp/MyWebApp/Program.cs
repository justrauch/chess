using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

bool isTest = false;
string dbName = "chess" + (isTest ? "_test" : "");
var connectionString = $"server=localhost;database={dbName};user=root;password=";

// DbContext definieren
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(connectionString));

// Session konfigurieren
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// CORS erlauben
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // React-App-Adresse
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware Reihenfolge
app.UseCors("AllowReactApp");
app.UseSession();

// Signup
app.MapPost("/signup", async (UserLogin login, AppDbContext db) =>
{
    try
    {
        bool exists = await db.Users.AnyAsync(u => u.name == login.Username);
        if (exists)
            return Results.Conflict(new { detail = "Benutzername bereits vergeben" });

        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(login.Password);

        var user = new User
        {
            name = login.Username,
            password = hashedPassword
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Benutzer erstellt" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fehler beim Signup: {ex.Message}");
        return Results.Problem("Interner Serverfehler");
    }
});

// Login
app.MapPost("/login", async (UserLogin login, AppDbContext db, HttpContext http) =>
{
    try
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.name == login.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(login.Password, user.password))
            return Results.Unauthorized();

        // Session setzen
        http.Session.SetString("UserId", user.id.ToString());
        http.Session.SetString("Username", user.name);

        return Results.Ok(new { message = "Login erfolgreich" });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Fehler beim Login: {ex.Message}");
        return Results.Problem("Interner Serverfehler");
    }
});

// Me-Route (aktueller Benutzer)
app.MapGet("/me", (HttpContext http) =>
{
    var userId = http.Session.GetString("UserId");
    var username = http.Session.GetString("Username");

    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    return Results.Ok(new { Id = userId, Username = username });
});

// Logout
app.MapPost("/logout", (HttpContext http) =>
{
    http.Session.Clear();
    return Results.Ok(new { message = "Erfolgreich ausgeloggt" });
});

app.Run();

// Models
public class User
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
}

public record UserLogin(string Username, string Password);
