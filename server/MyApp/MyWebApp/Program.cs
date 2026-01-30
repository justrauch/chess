using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

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

// Logout
app.MapPost("/logout", (HttpContext http) =>
{
    http.Session.Clear();
    return Results.Ok(new { message = "Erfolgreich ausgeloggt" });
});

List<int> queue = new List<int>();

// search for a Match
app.MapPost("/searchMatch", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    // Prüfen, ob Spieler schon in einem Match ist
    var matchexists = await db.Matches.AnyAsync(m => m.white_id == userId || m.black_id == userId);
    if(matchexists) return Results.Ok(new { message = "Match existiert bereits!" });

    // Spieler suchen oder warten
    if (queue.Count > 0)
    {
        int userId2 = queue[0];
        if (userId2 == userId)
            return Results.Ok(new { message = "Ein Spieler wird gesucht." });

        // Beide Spieler existieren?
        var usersexists = await db.Users.CountAsync(u => u.id == userId || u.id == userId2) == 2;
        if (!usersexists)
            return Results.Conflict(new { detail = "Internal Error" });

        Random random = new Random();
        int zahl = random.Next(0, 2);

        var match = new Match
        {
            white_id = zahl == 0 ? userId : userId2,
            black_id = zahl == 0 ? userId2 : userId,
            status = "active",
            game_state = "{ \"fen\": \"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1\", \"moveHistory\": [], \"lastMove\": null }"
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        queue.RemoveAt(0);

        return Results.Ok(new { message = "Match wurde gefunden!" });
    }
    else
    {
        if (!queue.Contains(userId))
            queue.Add(userId);

        return Results.Ok(new { message = "Ein Spieler wird gesucht." });
    }
});

app.MapGet("/getQueueState", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var matchexists = await db.Matches.AnyAsync(m => m.white_id == userId || m.black_id == userId);
    if(matchexists) return Results.Ok(new { message = "Match existiert bereits!" });
    else if (queue.Contains(userId)) return Results.Ok(new { message = "Spieler wird gesucht!" });
    else return Results.Ok(new { message = "Noch nicht gesucht!" });
});

app.MapGet("/getGameState", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(m => m.white_id == userId || m.black_id == userId);

    if (match != null)
    {
        var gameState = JsonSerializer.Deserialize<JsonElement>(match.game_state);

        string fen = gameState.GetProperty("fen").GetString() ?? "";
        string sideToMove = fen.Split(' ')[1];
        bool yourTurn = (sideToMove == "w" && match.white_id == userId) ||
                        (sideToMove == "b" && match.black_id == userId);

        return Results.Ok(new 
        { 
            game_state = fen.Split(' ')[0], 
            your_turn = yourTurn ? "true" : "false" 
        });
    }
    else
    {
        return Results.Ok(new { game_state = "", your_turn = "" });
    }
});

app.Run();

// Models
public class User
{
    [Key]
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

public class Match
{
    [Key]
    public int match_id { get; set; }
    public int white_id { get; set; }
    public int black_id { get; set; }
    public string game_state { get; set; } = "{}";
    public string status { get; set; } = string.Empty; // z.B. "waiting", "active", "finished"
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Match> Matches => Set<Match>();
}


public record UserLogin(string Username, string Password);
