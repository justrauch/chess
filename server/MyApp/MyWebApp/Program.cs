using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Security.Cryptography;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Reflection.Metadata;

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
    var matchexists = await db.Matches.AnyAsync(m => (m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active));
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
            white_active = true,
            black_active = true,
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

    var matchexists = await db.Matches.AnyAsync(m => (m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active));
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

    var match = await db.Matches.FirstOrDefaultAsync(m =>(m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active));

    if (match != null)
    {
        var gameState = JsonSerializer.Deserialize<JsonElement>(match.game_state);

        string fen = gameState.GetProperty("fen").GetString() ?? "";
        string sideToMove = fen.Split(' ')[1];
        bool yourTurn = (sideToMove == "w" && match.white_id == userId) ||
                        (sideToMove == "b" && match.black_id == userId);


        var winner = await db.Users.FirstOrDefaultAsync(u => u.id == match.winner_id);

        return Results.Ok(new 
        { 
            game_state = fen.Split(' ')[0], 
            your_turn = winner == null && yourTurn ? "true" : "false",
            your_color = match.white_id == userId ? "white" : "black",
            game_status = match.status,
            winner = winner != null ? winner.name : "none yet"
        });
    }
    else
    {
        return Results.Ok(new { game_state = "", your_turn = "" });
    }
});

app.MapPost("/MakeMove", async (Move move, HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(m => (m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active));
    if (match == null)
        return Results.BadRequest(new { message = "Kein aktives Spiel gefunden!" });

    var game = JsonSerializer.Deserialize<GameState>(match.game_state);
    if (game == null)
        return Results.BadRequest(new { message = "GameState konnte nicht geladen werden!" });

    if (!(match.white_active && match.black_active))
        return Results.Json(
            new { message = "Das Spiel wurde beendet!" },
            statusCode: 401
        );

    if (string.IsNullOrEmpty(game.fen)) return Results.BadRequest(new { message = match.game_state });

    string fen = game.fen.Split(" ")[0]; // nur das Board
    string[] fenRows = fen.Split("/");

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

    string playerColor = game.fen.Split(" ")[1]; // "w" oder "b"

    if ((playerColor == "w" && match.black_id == userId) || (playerColor == "b" && match.white_id == userId))
        return Results.Unauthorized();

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
                    if (m.y == 1 && m.ynew == m.y + 2 && target == '-' && IsPathClear(b, m.x, m.y, m.xnew, m.ynew)) return true; // start
                    if (movex == 0 && m.ynew == m.y + 1 && target == '-') {return true;} // normal
                    if (movex == 1 && m.ynew == m.y + 1 && target != '-' && char.IsUpper(target)) return true; // schlagen
                }
                else
                {
                    if (m.y == 6 && m.ynew == m.y - 2 && target == '-' && IsPathClear(b, m.x, m.y, m.xnew, m.ynew)) return true;
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
                    case 'r': shapeValid = movex == 0 || movey == 0; break;
                    case 'b': shapeValid = movex == movey; break;
                    case 'q': shapeValid = movex == 0 || movey == 0 || movex == movey; break;
                    case 'k': shapeValid = movex <= 1 && movey <= 1; break;
                }

                if (shapeValid && IsPathClear(b, m.x, m.y, m.xnew, m.ynew)) return true;
                break;
        }

        return false;
    }

    if (!IsValidMove(board, move))
            return Results.Json(
                new { message = "Dieser Zug entspricht nicht den Regeln!" },
                statusCode: 401
            );

    char piece = board[move.y, move.x];

    bool isPawnPromotion = (char.ToLower(piece) == 'p') &&
                        ((piece == 'P' && move.ynew == 0 && playerColor == "w") ||
                        (piece == 'p' && move.ynew == 7 && playerColor == "b"));

    board[move.ynew, move.xnew] = isPawnPromotion && !string.IsNullOrEmpty(move.new_piece)
        ? move.new_piece[0]
        : piece;

    board[move.y, move.x] = '-';

    Coordinate wK = new Coordinate { x = 0, y = 0 };
    Coordinate bK = new Coordinate { x = 0, y = 0 };
    List<Coordinate> wp = new List<Coordinate>();
    List<Coordinate> bp = new List<Coordinate>();

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
                if (board[y, x] == 'k') bK = new Coordinate {x = x, y = y};
                else if (board[y, x] == 'K') wK = new Coordinate {x = x, y = y};
                else if(char.IsLower(board[y, x])) bp.Add(new Coordinate {x = x, y = y});
                else wp.Add(new Coordinate {x = x, y = y});

                if (emptyCount > 0)
                {
                    fenRow += emptyCount;
                    emptyCount = 0;
                }

                fenRow += board[y, x];
            }
        }
        if (emptyCount > 0) fenRow += emptyCount;
        return fenRow;
    }));

    bool InBounds(int x, int y)
    {
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }

    bool CheckRay(char[,] b, int kx, int ky, int xv, int yv, string threats)
    {
        for (int i = 1; i < 8; i++)
        {
            int nx = kx + i * xv;
            int ny = ky + i * yv;

            if (!InBounds(nx, ny))
                return false;

            char p = b[ny, nx];

            if (p == '-')
                continue;

            if (threats.Contains(p))
                return true;

            return false; // Blockiert von anderer Figur
        }

        return false;
    }

    bool IsKingInCheck(char[,] b, int kx, int ky, string color)
    {
        bool isWhite = color == "w";
        int dir = isWhite ? -1 : 1;

        // Pawn
        int[] px = { -1, 1 };
        foreach (var dx in px)
            if (InBounds(kx + dx, ky + dir) &&
                b[ky + dir, kx + dx] == (isWhite ? 'p' : 'P'))
                return true;

        // Knight
        int[] nx = { -2,-2,-1,-1,1,1,2,2 };
        int[] ny = { -1,1,-2,2,-2,2,-1,1 };
        for (int i = 0; i < 8; i++)
            if (InBounds(kx + nx[i], ky + ny[i]) &&
                b[ky + ny[i], kx + nx[i]] == (isWhite ? 'n' : 'N'))
                return true;

        string rq = isWhite ? "rq" : "RQ";
        string bq = isWhite ? "bq" : "BQ";
        return 
            // Rook / Queen
            CheckRay(b, kx, ky, 1, 0, rq) || 
            CheckRay(b, kx, ky, -1, 0, rq) || 
            CheckRay(b, kx, ky, 0, 1, rq) || 
            CheckRay(b, kx, ky, 0, -1, rq) ||
            // Bishop / Queen
            CheckRay(b, kx, ky, 1, 1, bq) || 
            CheckRay(b, kx, ky, -1, 1, bq) || 
            CheckRay(b, kx, ky, 1, -1, bq) || 
            CheckRay(b, kx, ky, -1, -1, bq)||
            false;
    }

    bool hasAnyLegalMove(char[,] b, int kx, int ky, List<Coordinate> pieces, string color)
    {
        // Nur Königszüge
        int[] dx = Array.Empty<int>();
        int[] dy = Array.Empty<int>();

        var allPieces = new List<Coordinate>(pieces);
        allPieces.Add(new Coordinate { x = kx, y = ky });

        foreach (Coordinate piece in allPieces)
        {
            int x = piece.x;
            int y = piece.y;
            char p = b[y, x];

            if (char.ToLower(p) == 'p')
            {
                int dir = char.IsUpper(p) ? -1 : 1;

                dx = new int[] { 0, 0, 1, -1 };
                dy = new int[] { dir, dir * 2, dir, dir };
            }
            else if(char.ToLower(p) == 'k')
            {
                dx = new int[] { -1,-1,-1,0,0,1,1,1 };
                dy = new int[] { -1,0,1,-1,1,-1,0,1 };
            }
            else if(char.ToLower(p) == 'n')
            {
                dx = new int[] { 1, -1, 1, -1, 2, 2, -2, -2};
                dy = new int[] { 2, 2, -2, -2, 1, -1, 1, -1};
            }
            else if("rq".Contains(char.ToLower(p)))
            {
                dx = new int[] { 0, 0, 1, -1};
                dy = new int[] { 1, -1, 0, 0};
            }
            else if("bq".Contains(char.ToLower(p)))
            {
                dx = new int[] { 1, 1, -1, -1};
                dy = new int[] { 1, -1, 1, -1};
            }

            for (int j = 0; j < dx.Length; j++)
            {
                int nx = dx[j];
                int ny = dy[j];

                for(int i = 1; i < 8; i++)
                {
                    if (!InBounds(x + nx * i, y + ny * i))
                        break;
                    
                    if(!IsValidMove(b, new Move{x = x, y = y, xnew = x + nx * i, ynew = y + ny * i, new_piece = "" }))
                    {
                        continue;
                    }

                    char[,] boardCopy = (char[,])b.Clone();

                    boardCopy[y + ny * i, x + nx * i] = boardCopy[y, x];
                    boardCopy[y, x] = '-';
                    
                    if (!IsKingInCheck(boardCopy, char.ToLower(p) == 'k' ? x + nx : kx, char.ToLower(p) == 'k' ? y + ny : ky, color))
                    {
                        return true;
                    }
                    
                    bool sliding = "rbq".Contains(char.ToLower(p));
                    if (!sliding) break;
                }
            }

        }

        return false;
    }

    bool im_in_check = IsKingInCheck(
        board,
        playerColor == "w" ? wK.x : bK.x,
        playerColor == "w" ? wK.y : bK.y,
        playerColor
    );

    if(im_in_check)
            return Results.Json(
                new { message = "Dein König steht im Schach!" },
                statusCode: 401
            );

    string opponentColor = playerColor == "w" ? "b" : "w";
    Coordinate opponentKing = opponentColor == "w" ? wK : bK;
    List<Coordinate> opponentPieces = opponentColor == "w" ? wp : bp;

    bool opponentInCheck = IsKingInCheck(board, opponentKing.x, opponentKing.y, opponentColor);
    bool opponentHasLegalMove = hasAnyLegalMove(board, opponentKing.x, opponentKing.y, opponentPieces, opponentColor);

    if (opponentInCheck)
    {
        if (!opponentHasLegalMove)
            match.status = "Checkmate"; // Gegner ist matt
        else
            match.status = "Check"; // Gegner steht im Schach
    }
    else
    {
        if (!opponentHasLegalMove)
            match.status = "Stalemate"; // Patt
        else
            match.status = "Active"; // Spiel läuft normal
    }

    var fenParts = game.fen.Split(' ');
    fenParts[0] = newFenBoard;
    fenParts[1] = fenParts[1] == "w" ? "b" : "w";

    string newFen = string.Join(' ', fenParts);

    game.fen = newFen;
    
    if (game.moveHistory == null)
    {
        game.moveHistory = new List<object>();
    }

    game.moveHistory.Add(move);

    game.lastMove = move;

    match.game_state = JsonSerializer.Serialize(game);
    match.winner_id = match.status.Contains("mate") ? userId : null;

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Zug erfolgreich!" });
});

app.MapPost("/leaveMatch", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(m => (m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active));

    if (match == null)
        return Results.BadRequest(new { message = "Kein aktives Spiel gefunden!" });

    // Wenn noch kein Gewinner -> Gegner gewinnt
    if (match.winner_id == null)
    {
        int opponentId = match.white_id == userId ? match.black_id : match.white_id;
        match.winner_id = opponentId;
    }

    // Spieler als inaktiv markieren
    if (match.white_id == userId)
    {
        match.white_active = false;
    }
    else
    {
        match.black_active = false;
    }

    if (match.white_active == false && match.black_active == false)
    {
        match.status = "Inactive";
    }
    else{
        match.status = "Abandoned";
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Erfolgreich verlassen!" });
});

app.MapGet("/getallMatches/user", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var matches = await db.Matches
        .Where(m => (m.white_id == userId || m.black_id == userId) && m.status == "Inactive")
        .ToListAsync();

    if (matches.Count == 0)
        return Results.BadRequest(new { message = "Kein Spiel gefunden!" });

    var list = new List<Match_History_Element>();

    foreach (Match m in matches)
    {
        var game = JsonSerializer.Deserialize<GameState>(m.game_state);
        var winner = await db.Users.FirstOrDefaultAsync(u => u.id == m.winner_id);
        var loser = await db.Users.FirstOrDefaultAsync(u => u.id == (m.winner_id == m.white_id ? m.black_id : m.white_id));
        list.Add(new Match_History_Element
        {
            Winner = winner?.name ?? "unknown",
            Loser = loser?.name ?? "unknown",
            Anz_Moves = game?.moveHistory?.Count ?? 0
        });
    }

    string json = JsonSerializer.Serialize(list);
    Console.WriteLine(json);

    return Results.Ok(new { message = json });
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
    public bool white_active { get; set; } = true;
    public bool black_active { get; set; } = true;
    public string game_state { get; set; } = "{}";
    public string status { get; set; } = string.Empty; // z.B. "waiting", "active", "finished"
    public int? winner_id { get; set; }
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

public class Coordinate
{
    public int x { get; set; }
    public int y { get; set; }
}

public class Match_History_Element
{
    public string Winner { get; set; }
    public string Loser { get; set; }
    public int Anz_Moves { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Match> Matches => Set<Match>();
}


public record UserLogin(string Username, string Password);
