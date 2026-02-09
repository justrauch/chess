using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Security.Cryptography;
using System.Drawing;
using System.Security.Cryptography.X509Certificates;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Mvc;

// --------------------------------------------------------------------------------------------------------------------
// WebApplication Builder & Konfiguration
// --------------------------------------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

bool isTest = false;
string dbName = "chess" + (isTest ? "_test" : "");
var connectionString = $"server=localhost;database={dbName};user=root;password=";

// DbContext hinzufügen
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

// CORS für React-App erlauben
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware Reihenfolge: CORS → Session
app.UseCors("AllowReactApp");
app.UseSession();

// --------------------------------------------------------------------------------------------------------------------
// Benutzerverwaltung: Signup, Login, Logout
// --------------------------------------------------------------------------------------------------------------------

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

// --------------------------------------------------------------------------------------------------------------------
// Matchmaking & Warteschlange
// --------------------------------------------------------------------------------------------------------------------
List<int> queue = new List<int>();

// Match suchen (PVP oder PVE)
app.MapPost("/searchMatch", async ([FromQuery(Name = "IsPVP")] bool IsPVP, HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    // Prüfen, ob Spieler bereits in einem Match ist
    var matchexists = await db.Matches.AnyAsync(m => 
        ((m.IsPVP == IsPVP) && 
        ((m.white_id == userId && m.white_active) || 
         (m.black_id != null && m.black_id == userId && m.black_active)))
    );
    if (matchexists) return Results.Ok(new { message = "Match existiert bereits!" });

    // Spieler suchen oder in Warteschlange setzen
    if (!IsPVP || queue.Count > 0)
    {
        int userId2 = !IsPVP ? 0 : queue[0];
        if (userId2 == userId)
            return Results.Ok(new { message = "Ein Spieler wird gesucht." });

        // Prüfen, ob beide Spieler existieren
        var usersexists = await db.Users.CountAsync(u => u.id == userId || u.id == userId2);
        if ((IsPVP && usersexists < 2) || (!IsPVP && usersexists < 1))
            return Results.Conflict(new { detail = "Internal Error" });

        // Zufällige Zuweisung von Weiß und Schwarz
        Random random = new Random();
        int zahl = random.Next(0, 2);

        // Neues Match erstellen
        var match = IsPVP ? new Match
        {
            IsPVP = IsPVP,
            white_id = zahl == 0 ? userId : userId2,
            black_id = zahl == 0 ? userId2 : userId,
            white_active = true,
            black_active = true,
            status = "active",
            game_state = "{ \"fen\": \"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1\", \"moveHistory\": [], \"lastMove\": null }"
        } : new Match
        {
            IsPVP = IsPVP,
            white_id = userId,
            white_active = true,
            black_active = true,
            status = "active",
            game_state = "{ \"fen\": \"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1\", \"moveHistory\": [], \"lastMove\": null }"
        };

        db.Matches.Add(match);
        await db.SaveChangesAsync();

        if (IsPVP) queue.RemoveAt(0);

        return Results.Ok(new { message = "Match wurde gefunden!" });
    }
    else
    {
        if (!queue.Contains(userId))
            queue.Add(userId);

        return Results.Ok(new { message = "Ein Spieler wird gesucht." });
    }
});

// Queue Status abfragen
app.MapGet("/getQueueState", async ([FromQuery(Name = "IsPVP")] bool IsPVP, HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var matchexists = await db.Matches.AnyAsync(m => 
        m.IsPVP == IsPVP && 
        ((m.white_id == userId && m.white_active) || (m.black_id == userId && m.black_active))
    );

    if (matchexists) return Results.Ok(new { message = "Match existiert bereits!" });
    else if (queue.Contains(userId)) return Results.Ok(new { message = "Spieler wird gesucht!" });
    else return Results.Ok(new { message = "Noch nicht gesucht!" });
});

// --------------------------------------------------------------------------------------------------------------------
// Spielstatus & Züge
// --------------------------------------------------------------------------------------------------------------------
app.MapGet("/getGameState", async ([FromQuery(Name = "IsPVP")] bool IsPVP, HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(
        m => m.IsPVP == IsPVP &&
            ((m.white_id == userId && m.white_active) ||
             (m.black_id != null && m.black_id == userId && m.black_active))
    );

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

// --------------------------------------------------------------------------------------------------------------------
// Schachlogik: Zugüberprüfung
// --------------------------------------------------------------------------------------------------------------------

// Prüft, ob der Weg zwischen zwei Feldern frei ist
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

// Prüft, ob ein gegebener Zug für eine Figur gültig ist
bool IsValidMove(char[,] b, Move m)
{
    char piece = b[m.y, m.x];
    char target = b[m.ynew, m.xnew];

    if (target != '-' && char.IsUpper(target) == char.IsUpper(piece))
        return false;

    int movex = Math.Abs(m.xnew - m.x);
    int movey = Math.Abs(m.ynew - m.y);

    // Grenzen prüfen
    if (m.xnew < 0 || m.xnew >= 8 || m.ynew < 0 || m.ynew >= 8)
        return false;

    // Figuren-spezifische Regeln
    switch (char.ToLower(piece))
    {
        case 'p': // Bauer
            if (char.IsLower(piece))
            {
                if (m.y == 1 && m.ynew == m.y + 2 && target == '-' && IsPathClear(b, m.x, m.y, m.xnew, m.ynew)) return true;
                if (movex == 0 && m.ynew == m.y + 1 && target == '-') return true;
                if (movex == 1 && m.ynew == m.y + 1 && target != '-' && char.IsUpper(target)) return true;
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

// Prüft, ob Koordinaten im Spielfeld liegen
bool InBounds(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;

// Prüft, ob auf einem Strahl eine bedrohte Figur liegt
bool CheckRay(char[,] b, int kx, int ky, int xv, int yv, string threats)
{
    for (int i = 1; i < 8; i++)
    {
        int nx = kx + i * xv;
        int ny = ky + i * yv;

        if (!InBounds(nx, ny))
            return false;

        char p = b[ny, nx];

        if (p == '-') continue;

        if (threats.Contains(p)) return true;

        return false; // Blockiert von anderer Figur
    }

    return false;
}

// Prüft, ob ein König im Schach steht
bool IsKingInCheck(char[,] b, int kx, int ky, string color)
{
    bool isWhite = color == "w";
    int dir = isWhite ? -1 : 1;

    // Bauer
    int[] px = { -1, 1 };
    foreach (var dx in px)
        if (InBounds(kx + dx, ky + dir) &&
            b[ky + dir, kx + dx] == (isWhite ? 'p' : 'P'))
            return true;

    // Springer
    int[] nx = { -2, -2, -1, -1, 1, 1, 2, 2 };
    int[] ny = { -1, 1, -2, 2, -2, 2, -1, 1 };
    for (int i = 0; i < 8; i++)
        if (InBounds(kx + nx[i], ky + ny[i]) &&
            b[ky + ny[i], kx + nx[i]] == (isWhite ? 'n' : 'N'))
            return true;

    string rq = isWhite ? "rq" : "RQ";
    string bq = isWhite ? "bq" : "BQ";

    // Türme, Läufer, Damen prüfen
    return
        CheckRay(b, kx, ky, 1, 0, rq) ||
        CheckRay(b, kx, ky, -1, 0, rq) ||
        CheckRay(b, kx, ky, 0, 1, rq) ||
        CheckRay(b, kx, ky, 0, -1, rq) ||
        CheckRay(b, kx, ky, 1, 1, bq) ||
        CheckRay(b, kx, ky, -1, 1, bq) ||
        CheckRay(b, kx, ky, 1, -1, bq) ||
        CheckRay(b, kx, ky, -1, -1, bq) ||
        false;
}
// --------------------------------------------------------------------------------------------------------------------
// Prüft, ob eine Figur oder der König noch legale Züge hat
// --------------------------------------------------------------------------------------------------------------------
bool hasAnyLegalMove(char[,] b, int kx, int ky, List<Coordinate> pieces, string color)
{
    // Arrays für Zugrichtungen initialisieren
    int[] dx = Array.Empty<int>();
    int[] dy = Array.Empty<int>();

    // Alle Figuren inklusive König berücksichtigen
    var allPieces = new List<Coordinate>(pieces);
    allPieces.Add(new Coordinate { x = kx, y = ky });

    foreach (Coordinate piece in allPieces)
    {
        int x = piece.x;
        int y = piece.y;
        char p = b[y, x];

        // Zugrichtungen für die jeweilige Figur bestimmen
        if (char.ToLower(p) == 'p') // Bauer
        {
            int dir = char.IsUpper(p) ? -1 : 1;
            dx = new int[] { 0, 0, 1, -1 };
            dy = new int[] { dir, dir * 2, dir, dir };
        }
        else if (char.ToLower(p) == 'k') // König
        {
            dx = new int[] { -1, -1, -1, 0, 0, 1, 1, 1 };
            dy = new int[] { -1, 0, 1, -1, 1, -1, 0, 1 };
        }
        else if (char.ToLower(p) == 'n') // Springer
        {
            dx = new int[] { 1, -1, 1, -1, 2, 2, -2, -2 };
            dy = new int[] { 2, 2, -2, -2, 1, -1, 1, -1 };
        }
        else if ("rq".Contains(char.ToLower(p))) // Turm/Dame
        {
            dx = new int[] { 0, 0, 1, -1 };
            dy = new int[] { 1, -1, 0, 0 };
        }
        else if ("bq".Contains(char.ToLower(p))) // Läufer/Dame
        {
            dx = new int[] { 1, 1, -1, -1 };
            dy = new int[] { 1, -1, 1, -1 };
        }

        // Prüfe alle möglichen Züge
        for (int j = 0; j < dx.Length; j++)
        {
            int nx = dx[j];
            int ny = dy[j];

            for (int i = 1; i < 8; i++)
            {
                if (!InBounds(x + nx * i, y + ny * i))
                    break;

                if (!IsValidMove(b, new Move { x = x, y = y, xnew = x + nx * i, ynew = y + ny * i, new_piece = "" }))
                    continue;

                char[,] boardCopy = (char[,])b.Clone();
                boardCopy[y + ny * i, x + nx * i] = boardCopy[y, x];
                boardCopy[y, x] = '-';

                // Prüfen, ob der König nach dem Zug im Schach steht
                if (!IsKingInCheck(boardCopy, char.ToLower(p) == 'k' ? x + nx : kx,
                                                char.ToLower(p) == 'k' ? y + ny : ky, color))
                {
                    return true;
                }

                bool sliding = "rbq".Contains(char.ToLower(p));
                if (!sliding) break; // Nicht-sliding Figuren brechen nach einem Schritt
            }
        }
    }

    return false; // Keine legalen Züge gefunden
}

// --------------------------------------------------------------------------------------------------------------------
// Rekursive Minimax-artige Funktion für PVE-Züge
// --------------------------------------------------------------------------------------------------------------------
Eval makealllegalMoves(char[,] b, bool IsPVE, int depth)
{
    // Tiefenlimit erreicht → Bewertungsfunktion
    if (depth == 5)
    {
        int score = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                char element = b[y, x];
                int piece_value = GetPieceValue(element);

                if (char.IsLower(element))
                    score += IsPVE ? piece_value : piece_value * -1;
                else
                    score += !IsPVE ? piece_value : piece_value * -1;
            }
        }
        return new Eval { score = score, move = null };
    }

    // Alle Figuren des Spielers und des Gegners sammeln
    List<Coordinate> allPieces = new List<Coordinate>();
    int kx = 0, ky = 0;
    List<Coordinate> oallPieces = new List<Coordinate>();
    int okx = 0, oky = 0;

    for (int y = 0; y < 8; y++)
    {
        for (int x = 0; x < 8; x++)
        {
            char element = b[y, x];

            if (IsPVE && char.IsLower(element))
            {
                if (element == 'k') { kx = x; ky = y; }
                allPieces.Add(new Coordinate { x = x, y = y });
            }
            else if (!IsPVE && char.IsUpper(element))
            {
                if (element == 'K') { kx = x; ky = y; }
                allPieces.Add(new Coordinate { x = x, y = y });
            }
            else
            {
                if (char.ToLower(element) == 'k') { okx = x; oky = y; }
                oallPieces.Add(new Coordinate { x = x, y = y });
            }
        }
    }

    int[] dx = Array.Empty<int>();
    int[] dy = Array.Empty<int>();
    int bestscore = IsPVE ? int.MinValue : int.MaxValue;
    char movepiece = '-';
    Move? bestmove = null;

    foreach (Coordinate piece in allPieces)
    {
        int x = piece.x;
        int y = piece.y;
        char p = b[y, x];

        // Zugrichtungen nach Figur festlegen
        if (char.ToLower(p) == 'p') { int dir = char.IsUpper(p) ? -1 : 1; dx = new int[] { 0, 0, 1, -1 }; dy = new int[] { dir, dir * 2, dir, dir }; }
        else if (char.ToLower(p) == 'k') { dx = new int[] { -1, -1, -1, 0, 0, 1, 1, 1 }; dy = new int[] { -1, 0, 1, -1, 1, -1, 0, 1 }; }
        else if (char.ToLower(p) == 'n') { dx = new int[] { 1, -1, 1, -1, 2, 2, -2, -2 }; dy = new int[] { 2, 2, -2, -2, 1, -1, 1, -1 }; }
        else if ("rq".Contains(char.ToLower(p))) { dx = new int[] { 0, 0, 1, -1 }; dy = new int[] { 1, -1, 0, 0 }; }
        else if ("bq".Contains(char.ToLower(p))) { dx = new int[] { 1, 1, -1, -1 }; dy = new int[] { 1, -1, 1, -1 }; }

        // Alle Züge testen
        for (int j = 0; j < dx.Length; j++)
        {
            int nx = dx[j], ny = dy[j];

            for (int i = 1; i < 8; i++)
            {

                bool isPromotion = char.ToLower(p) == 'p' &&
                   ((char.IsUpper(p) && y + ny * i == 0) ||
                    (char.IsLower(p) && y + ny * i == 7));

                if (!InBounds(x + nx * i, y + ny * i))
                    break;

                if (!IsValidMove(b, new Move { x = x, y = y, xnew = x + nx * i, ynew = y + ny * i, new_piece = isPromotion ? (IsPVE ? "q" : "Q") : "" }))
                    continue;

                int score = 0;

                if (isPromotion)
                {
                    score += 50;
                }

                char[,] boardCopy = (char[,])b.Clone();

                // Capture Bonus
                if (boardCopy[y + ny * i, x + nx * i] != '-') score += 5;

                boardCopy[y + ny * i, x + nx * i] = isPromotion ? (IsPVE ? 'q' : 'Q') : boardCopy[y, x];
                boardCopy[y, x] = '-';

                // König prüfen
                if (IsKingInCheck(boardCopy, char.ToLower(p) == 'k' ? x + nx : kx,
                                            char.ToLower(p) == 'k' ? y + ny : ky, IsPVE ? "b" : "w"))
                    continue;

                bool incheck = IsKingInCheck(boardCopy, okx, oky, IsPVE ? "w" : "b");
                bool hasmoves = hasAnyLegalMove(boardCopy, okx, oky, oallPieces, IsPVE ? "w" : "b");

                // Punkte für Check
                if (incheck) score += 20;

                // Bonus für Zentrum
                if ((y + ny * i > 1 && y + ny * i < 6) || (x + nx * i > 1 && x + nx * i < 6)) score += 5;

                if (!hasmoves) score = !IsPVE ? int.MinValue : int.MaxValue;
                else score += makealllegalMoves(boardCopy, !IsPVE, depth + 1).score;

                // Besten Zug auswählen
                if (IsPVE ? (score >= bestscore && HasHigherPriority(movepiece, p)) :
                             (score <= bestscore && !HasHigherPriority(movepiece, p)))
                {
                    bestscore = score;
                    movepiece = p;
                    bestmove = new Move { x = x, y = y, xnew = x + nx * i, ynew = y + ny * i, new_piece = "" };
                }

                bool sliding = "rbq".Contains(char.ToLower(p));
                if (!sliding || (hasmoves)) break;
            }
        }
    }

    return new Eval { score = bestscore, move = bestmove };
}

// --------------------------------------------------------------------------------------------------------------------
// Hilfsfunktionen für Priorität & Bewertung
// --------------------------------------------------------------------------------------------------------------------
int GetPiecePriority(char piece) => char.ToLower(piece) switch
{
    'p' => 1,
    'n' => 2,
    'b' => 3,
    'q' => 4,
    'r' => 5,
    'k' => 6,
    _ => 100
};

bool HasHigherPriority(char oldPiece, char newPiece) => GetPiecePriority(newPiece) < GetPiecePriority(oldPiece);

int GetPieceValue(char p) => char.ToLower(p) switch
{
    'p' => 1,
    'n' => 3,
    'b' => 3,
    'r' => 5,
    'q' => 9,
    'k' => 1000,
    _ => 0
};

// --------------------------------------------------------------------------------------------------------------------
// API-Endpunkte für AI-Züge & Tests
// --------------------------------------------------------------------------------------------------------------------
app.MapPost("/MakeMoveAI", async (FenRequest fen, AppDbContext db) =>
{
    Move ret = PVEMove(fen.fen);
    return Results.Ok(new { message = JsonSerializer.Serialize(ret) });
});

// Prüft, ob legale Züge existieren (Testzwecke)
app.MapPost("/haslegalmoves", async (FenRequest fen, AppDbContext db) =>
{
    string gfen = fen.fen.Split(" ")[0];
    string[] fenRows = gfen.Split("/");

    char[,] board = new char[8, 8];
    List<Coordinate> allPieces = new List<Coordinate>();
    int kx = 0, ky = 0;

    for (int y = 0; y < 8; y++)
    {
        int xIndex = 0;
        foreach (var ch in fenRows[y])
        {
            if (ch == 'K') { kx = xIndex; ky = y; }
            if (char.IsUpper(ch)) allPieces.Add(new Coordinate { x = xIndex, y = y });

            if (char.IsDigit(ch))
            {
                int empty = int.Parse(ch.ToString());
                for (int i = 0; i < empty; i++) board[y, xIndex++] = '-';
            }
            else board[y, xIndex++] = ch;
        }
    }

    return Results.Ok(new { message = hasAnyLegalMove(board, kx, ky, allPieces, "w") });
});

// --------------------------------------------------------------------------------------------------------------------
// Hilfsfunktion: Berechnet besten AI-Zug aus FEN
// --------------------------------------------------------------------------------------------------------------------
Move PVEMove(string fen)
{
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
                for (int i = 0; i < empty; i++) board[y, xIndex++] = '-';
            }
            else board[y, xIndex++] = ch;
        }
    }

    return makealllegalMoves(board, true, 1).move;
}


// --------------------------------------------------------------------------------------------------------------------
// WebAPI Endpunkte für Schachspiel (PVP / PVE)
// --------------------------------------------------------------------------------------------------------------------

// Spielerzug ausführen
app.MapPost("/MakeMove", async ([FromQuery(Name = "IsPVP")] bool IsPVP, MakeMove mm, HttpContext http, AppDbContext db) =>
{
    Move move = mm.move;

    // User-ID aus Session abrufen
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    // Aktives Match des Users suchen
    var match = await db.Matches.FirstOrDefaultAsync(m =>
        m.IsPVP == IsPVP &&
        ((m.white_id == userId && m.white_active) || (m.black_id != null && m.black_id == userId && m.black_active))
    );

    if (match == null)
        return Results.BadRequest(new { message = "Kein aktives Spiel gefunden!" });

    var game = JsonSerializer.Deserialize<GameState>(match.game_state);
    if (game == null)
        return Results.BadRequest(new { message = "GameState konnte nicht geladen werden!" });

    if (!(match.white_active && match.black_active))
        return Results.Json(new { message = "Das Spiel wurde beendet!" }, statusCode: 401);

    if (string.IsNullOrEmpty(game.fen))
        return Results.BadRequest(new { message = match.game_state });

    string[] fenfull = game.fen.Split(" ");
    string fen = fenfull[0]; // Nur das Board

    // KI-Zug berechnen, wenn PVE und am Zug
    if (!mm.IsPVP && fenfull[1] == "b")
        move = PVEMove(fen);

    // FEN in Board umwandeln
    char[,] board = new char[8, 8];
    for (int y = 0; y < 8; y++)
    {
        int xIndex = 0;
        foreach (var ch in fen.Split("/")[y])
        {
            if (char.IsDigit(ch))
            {
                int empty = int.Parse(ch.ToString());
                for (int i = 0; i < empty; i++)
                    board[y, xIndex++] = '-';
            }
            else
            {
                board[y, xIndex++] = ch;
            }
        }
    }

    string playerColor = fenfull[1]; // "w" oder "b"

    // Zugberechtigungsprüfung
    if ((playerColor == "w" && match.black_id == userId) || (mm.IsPVP && playerColor == "b" && match.white_id == userId))
        return Results.Unauthorized();

    // Zugregelprüfung
    if (!IsValidMove(board, move))
        return Results.Json(new { message = "Dieser Zug entspricht nicht den Regeln!" }, statusCode: 401);

    char piece = board[move.y, move.x];

    // Bauernumwandlung prüfen
    bool isPawnPromotion = (char.ToLower(piece) == 'p') &&
                        ((piece == 'P' && move.ynew == 0 && playerColor == "w") ||
                         (piece == 'p' && move.ynew == 7 && playerColor == "b"));

    board[move.ynew, move.xnew] = isPawnPromotion && !string.IsNullOrEmpty(move.new_piece)
        ? move.new_piece[0]
        : piece;

    board[move.y, move.x] = '-';

    // König- und Figurenkoordinaten sammeln
    Coordinate wK = new Coordinate { x = 0, y = 0 };
    Coordinate bK = new Coordinate { x = 0, y = 0 };
    List<Coordinate> wp = new List<Coordinate>();
    List<Coordinate> bp = new List<Coordinate>();

    // Neues FEN erstellen
    string newFenBoard = string.Join("/", Enumerable.Range(0, 8).Select(y =>
    {
        int emptyCount = 0;
        string fenRow = "";
        for (int x = 0; x < 8; x++)
        {
            if (board[y, x] == '-')
            {
                emptyCount++;
            }
            else
            {
                if (board[y, x] == 'k') bK = new Coordinate { x = x, y = y };
                else if (board[y, x] == 'K') wK = new Coordinate { x = x, y = y };
                else if (char.IsLower(board[y, x])) bp.Add(new Coordinate { x = x, y = y });
                else wp.Add(new Coordinate { x = x, y = y });

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

    // Prüfen, ob eigener König im Schach steht
    bool im_in_check = IsKingInCheck(board,
        playerColor == "w" ? wK.x : bK.x,
        playerColor == "w" ? wK.y : bK.y,
        playerColor
    );

    if (im_in_check)
        return Results.Json(new { message = "Dein König steht im Schach!" }, statusCode: 401);

    // Gegnerstatus prüfen
    string opponentColor = playerColor == "w" ? "b" : "w";
    Coordinate opponentKing = opponentColor == "w" ? wK : bK;
    List<Coordinate> opponentPieces = opponentColor == "w" ? wp : bp;

    bool opponentInCheck = IsKingInCheck(board, opponentKing.x, opponentKing.y, opponentColor);
    bool opponentHasLegalMove = hasAnyLegalMove(board, opponentKing.x, opponentKing.y, opponentPieces, opponentColor);

    if (opponentInCheck)
    {
        match.status = !opponentHasLegalMove ? "Checkmate" : "Check";
    }
    else
    {
        match.status = !opponentHasLegalMove ? "Stalemate" : "Active";
    }

    // FEN aktualisieren
    var fenParts = game.fen.Split(' ');
    fenParts[0] = newFenBoard;
    fenParts[1] = fenParts[1] == "w" ? "b" : "w";
    game.fen = string.Join(' ', fenParts);

    // Historie aktualisieren
    if (game.moveHistory == null)
        game.moveHistory = new List<object>();

    game.moveHistory.Add(move);
    game.lastMove = move;

    match.game_state = JsonSerializer.Serialize(game);
    match.winner_id = match.status.Contains("mate") ? userId : null;

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Zug erfolgreich!" });
});

// Spieler verlässt Match
app.MapPost("/leaveMatch", async (bool IsPVP, HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr))
        return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var match = await db.Matches.FirstOrDefaultAsync(m =>
        m.IsPVP == IsPVP &&
        ((m.white_id == userId && m.white_active) || (m.black_id != null && m.black_id == userId && m.black_active))
    );

    if (match == null)
        return Results.BadRequest(new { message = "Kein aktives Spiel gefunden!" });

    if (!match.IsPVP)
    {
        db.Matches.Remove(match);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "PVE Match erfolgreich gelöscht!" });
    }

    // Gegner gewinnt, falls noch kein Gewinner
    if (match.black_id.HasValue && match.winner_id == null)
    {
        int opponentId = match.white_id == userId ? match.black_id.Value : match.white_id;
        match.winner_id = opponentId;
    }

    // Spieler inaktiv markieren
    if (match.white_id == userId) match.white_active = false;
    else match.black_active = false;

    match.status = (!match.white_active && !match.black_active) ? "Inactive" : "Abandoned";

    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Erfolgreich verlassen!" });
});

// Alle abgeschlossenen Matches eines Users abfragen
app.MapGet("/getallMatches/user", async (HttpContext http, AppDbContext db) =>
{
    var userIdStr = http.Session.GetString("UserId");
    if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();

    int userId = int.Parse(userIdStr);

    var matches = await db.Matches
        .Where(m => (m.white_id == userId || m.black_id == userId) && (m.status == "Inactive" || m.status == "Abandoned"))
        .ToListAsync();

    if (matches.Count == 0) return Results.BadRequest(new { message = "Kein Spiel gefunden!" });

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

    return Results.Ok(new { message = JsonSerializer.Serialize(list) });
});

app.Run();

// --------------------------------------------------------------------------------------------------------------------
// Modelle / DbContext
// --------------------------------------------------------------------------------------------------------------------
public class User
{
    [Key] public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

public class Match
{
    [Key] public int match_id { get; set; }
    public bool IsPVP { get; set; } = true;
    public int white_id { get; set; }
    public int? black_id { get; set; }
    public bool white_active { get; set; } = true;
    public bool black_active { get; set; } = true;
    public string game_state { get; set; } = "{}";
    public string status { get; set; } = string.Empty;
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

public class Eval
{
    public int score { get; set; }
    public Move move { get; set; }
}

public class MakeMove
{
    public bool IsPVP { get; set; } = true;
    public Move move { get; set; }
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

public record FenRequest(string fen);
public record Search(bool IsPVP);
public record UserLogin(string Username, string Password);
