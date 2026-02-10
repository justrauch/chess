
# Project Setup & Commands

## Install Dependencies

### Frontend (React)
```bash
npm install -D @types/react-router-dom
```

### Backend (.NET)
```bash
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Pomelo.EntityFrameworkCore.MySql
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package BCrypt.Net-Next --version 4.0.2
```

## Create .NET Projects
```bash
dotnet new console -n MyApp
cd ./MyApp/
dotnet new webapp -n MyWebApp
```
## Database Setup

1. **Start MySQL in XAMPP**  
   - Öffne XAMPP **als Administrator**.  
   - Klicke auf **Start** bei MySQL.  

2. **Start HeidiSQL**  
   - Verbinde dich mit der laufenden MySQL-Instanz.  

3. **Set up Database using Python**  
```bash
cd /server
python db.py
```

## Run the Project

### Start Frontend (VS Code)
```bash
npm run dev
```

### Start Backend
```bash
cd ../server/MyApp/MyWebApp
dotnet run
```

## Chess Piece Images

**Base URL:**  
https://images.chesscomfiles.com/chess-themes/pieces/neo/150/wk.png

**Naming logic:**
- `w` → piece color (white)
- `b` → piece color (black)
- `k` → king
- `q` → queen
- `wk` → whiteKing
- `bq` → blackQueen

## Main Code Files

### Backend
```text
/server/MyApp/MyWebApp/Program.cs
```

### Database
```text
/server/db.py
```

### Frontend
```text
/chess/client/chess/src/main.tsx
/chess/client/chess/src/Home.tsx
/chess/client/chess/src/Chessboard.tsx
```
