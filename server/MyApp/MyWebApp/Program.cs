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
            your_turn = yourTurn ? "true" : "false",
            your_color = match.white_id == userId ? "white" : "black"
        });
    }
    else
    {
        return Results.Ok(new { game_state = "", your_turn = "" });
    }
});

app.MapPost("/MakeMove", async (Move move, HttpContext http, AppDbContext db) =>
{
    // 1️⃣ User prüfen
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(m => m.white_id == userId || m.black_id == userId);
    if (match == null)
        return Results.BadRequest(new { message = "Kein aktives Spiel gefunden!" });

    // 2️⃣ GameState laden
    var game = JsonSerializer.Deserialize<GameState>(match.game_state);
    if (game == null)
        return Results.BadRequest(new { message = "GameState konnte nicht geladen werden!" });

    if (string.IsNullOrEmpty(game.fen)) return Results.BadRequest(new { message = match.game_state });

    string fen = game.fen.Split(" ")[0]; // nur das Board
    string[] fenRows = fen.Split("/");

    // 3️⃣ Board als char[,] erstellen
    char[,] board = new char[8, 8];
    for (int y = 0; y < 8; y++)
    {
        int xIndex = 0;
        foreach (var ch in fenRows[y])
        {
            if (char.IsDigit(ch))
            {
                int empty = int.Parse(ch.ToString());
                for (int i = 0; i < empty; i++)
                {
                    board[y, xIndex++] = '-';
                }
            }
            else
            {
                board[y, xIndex++] = ch;
            }
        }
    }

    // 4️⃣ Spielerfarbe prüfen
    string playerColor = game.fen.Split(" ")[1]; // "w" oder "b"
    if ((playerColor == "w" && match.black_id == userId) || (playerColor == "b" && match.white_id == userId))
        return Results.Unauthorized();

    // 5️⃣ Prüfen, ob Move gültig ist
    bool IsPathClear(char[,] b, int x, int y, int xnew, int ynew)
    {
        int dx = xnew - x;
        int dy = ynew - y;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 1) return true;

        int stepX = dx == 0 ? 0 : dx / Math.Abs(dx);
        int stepY = dy == 0 ? 0 : dy / Math.Abs(dy);

        for (int i = 1; i < steps; i++)
        {
            if (b[y + i * stepY, x + i * stepX] != '-')
                return false;
        }

        return true;
    }

    bool IsValidMove(char[,] b, Move m)
    {
        char piece = b[m.y, m.x];
        char target = b[m.ynew, m.xnew];

        int movex = Math.Abs(m.xnew - m.x);
        int movey = Math.Abs(m.ynew - m.y);

        // Grenzen
        if (m.xnew < 0 || m.xnew >= 8 || m.ynew < 0 || m.ynew >= 8)
            return false;

        // Eigenes Feld
        if (target != '-' && char.IsUpper(target) == char.IsUpper(piece))
            return false;

        // Figuren-spezifische Regeln
        switch (char.ToLower(piece))
        {
            case 'p': // Bauer
                if (char.IsLower(piece))
                {
                    if (m.y == 1 && m.ynew == m.y + 2 && target == '-') return true;
                    if (movex == 0 && m.ynew == m.y + 1 && target == '-') return true; // normal
                    if (movex == 1 && m.ynew == m.y + 1 && target != '-' && char.IsUpper(target)) return true; // schlagen
                }
                else
                {
                    if (m.y == 6 && m.ynew == m.y - 2 && target == '-') return true;
                    if (movex == 0 && m.ynew == m.y - 1 && target == '-') return true;
                    if (movex == 1 && m.ynew == m.y - 1 && target != '-' && char.IsLower(target)) return true;
                }
                break;

            case 'n': // Springer
                if ((movex == 1 && movey == 2) || (movex == 2 && movey == 1)) return true;
                break;

            case 'r':
            case 'b':
            case 'q':
            case 'k':
                bool shapeValid = false;
                switch (char.ToLower(piece))
                {
                    case 'r': shapeValid = (movex == 0 || movey == 0); break;
                    case 'b': shapeValid = (movex == movey); break;
                    case 'q': shapeValid = (movex == 0 || movey == 0 || movex == movey); break;
                    case 'k': shapeValid = (movex <= 1 && movey <= 1); break;
                }

                if (shapeValid && IsPathClear(b, m.x, m.y, m.xnew, m.ynew)) return true;
                break;
        }

        return false;
    }

    if (!IsValidMove(board, move))
        return Results.Unauthorized();

    char piece = board[move.y, move.x];

    bool isPawnPromotion = (char.ToLower(piece) == 'p') &&
                        ((piece == 'P' && move.ynew == 0 && playerColor == "w") ||
                            (piece == 'p' && move.ynew == 7 && playerColor == "b"));

    board[move.ynew, move.xnew] = isPawnPromotion && !string.IsNullOrEmpty(move.new_piece)
        ? move.new_piece[0]
        : piece;

    board[move.y, move.x] = '-';

    // Neues FEN bauen
    string newFenBoard = string.Join("/", Enumerable.Range(0, 8).Select(y =>
    {
        int emptyCount = 0;
        string fenRow = "";
        for (int x = 0; x < 8; x++)
        {
            if (board[y, x] == '-')
                emptyCount++;
            else
            {
                if (emptyCount > 0) { fenRow += emptyCount; emptyCount = 0; }
                fenRow += board[y, x];
            }
        }
        if (emptyCount > 0) fenRow += emptyCount;
        return fenRow;
    }));

    // Rest der FEN bleibt gleich
    var fenParts = game.fen.Split(' ');
    fenParts[0] = newFenBoard;
    fenParts[1] = fenParts[1] == "w" ? "b" : "w";
    string newFen = string.Join(' ', fenParts);

    game.fen = newFen;
    match.game_state = JsonSerializer.Serialize(game);

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Zug erfolgreich!" });
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

public class Move
{
    public int x { get; set; }
    public int y { get; set; }
    public int xnew { get; set; }
    public int ynew { get; set; }
    public string new_piece { get; set; } = string.Empty;
}

public class GameState
{
    public string fen { get; set; }
    public List<object> moveHistory { get; set; }
    public object lastMove { get; set; }
}


public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Match> Matches => Set<Match>();
}


public record UserLogin(string Username, string Password);
