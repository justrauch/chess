import "./App.css";
import { useState, useEffect } from "react";

// Highlight-Typen für Felder
type Highlight = "neutral" | "move" | "capture" | "recruite";

// Ein einzelnes Brettfeld
type Cell = {
    piece: string | null;
    color: "white" | "black" | null;
    highlight: Highlight;
};

// Props vom Parent (Spielstatus)
type ChessboardProps = {
    board: string;
    mycolor: string;
    myturn: boolean;
    IsPVP: boolean;
};

// Move-Objekt für Zugübertragung
type Move = {
    methode: string;
    piece: string;
    x: number;
    y: number;
    xnew: number;
    ynew: number;
    newpiece: string;
};

export default function Chessboard({ board, mycolor, myturn, IsPVP }: ChessboardProps) {
    const [grid, setGrid] = useState<Cell[][]>([]);
    const [Move, setMove] = useState<Move>();

    // Fehleranzeige
    const [Error, setError] = useState("");
    const [ShowError, setShowError] = useState(false);

    // Hilfsfunktionen für Figurenfarbe
    const isUpper = (c: string) => c === c.toUpperCase() && c !== c.toLowerCase();
    const isLower = (c: string) => c === c.toLowerCase() && c !== c.toUpperCase();

    // FEN-String in internes Grid umwandeln
    useEffect(() => {
        const rows = board.split("/");
        const newGrid: Cell[][] = [];

        rows.forEach(row => {
            const rowArray: Cell[] = [];

            row.split("").forEach(char => {
                if (!isNaN(Number(char))) {
                    // Leere Felder aus Zahl erzeugen
                    const emptyCount = Number(char);
                    for (let i = 0; i < emptyCount; i++) {
                        rowArray.push({ piece: null, color: null, highlight: "neutral" });
                    }
                } else {
                    // Figur setzen
                    rowArray.push({
                        piece: char,
                        color: isUpper(char) ? "white" : "black",
                        highlight: "neutral",
                    });
                }
            });

            newGrid.push(rowArray);
        });

        setGrid(newGrid);
    }, [board]);

    // Mögliche Züge anzeigen
    const ShowMoves = (x: number, y: number) => {
        // Alle Highlights resetten
        setGrid(prev =>
            prev.map(row => row.map(cell => ({ ...cell, highlight: "neutral" as Highlight })))
        );

        setGrid(prev => {
            const newGrid = prev.map(row => row.map(cell => ({ ...cell })));
            const cell = newGrid[y][x];

            // Aktuellen Zug speichern
            setMove({
                methode: "",
                piece: cell.piece!,
                x: x,
                y: y,
                xnew: -1,
                ynew: -1,
                newpiece: ""
            });

            // Ungültige Klicks blockieren
            if (
                !cell.piece || 
                !myturn ||
                (
                    (mycolor === "white" && isLower(cell.piece || "")) || 
                    (mycolor === "black" && isUpper(cell.piece || ""))
                )
            ) return newGrid;

            // Bauernlogik (schwarz)
            if (cell.piece === "p") {
                // vorwärts
                if (y + 1 < 8 && !newGrid[y + 1][x].piece) {
                    if (y == 1 && !newGrid[y + 2][x].piece) newGrid[y + 2][x].highlight = "move";
                    newGrid[y + 1][x].highlight = "move";
                }

                // Promotion
                if (y + 1 == 7) newGrid[y + 1][x].highlight = "recruite";

                // Schlagen links
                if (y + 1 < 8 && x - 1 >= 0 && newGrid[y + 1][x - 1].piece && isUpper(newGrid[y + 1][x - 1].piece!))
                    newGrid[y + 1][x - 1].highlight = y + 1 == 7 ? "recruite": "capture";

                // Schlagen rechts
                if (y + 1 < 8 && x + 1 < 8 && newGrid[y + 1][x + 1].piece && isUpper(newGrid[y + 1][x + 1].piece!))
                    newGrid[y + 1][x + 1].highlight = y + 1 == 7 ? "recruite": "capture";
            }

            // Bauernlogik (weiß)
            else if (cell.piece === "P") {
                if (y - 1 > 0 && !newGrid[y - 1][x].piece) {
                    if (y == 6 && !newGrid[y - 2][x].piece) newGrid[y - 2][x].highlight = "move";
                    newGrid[y - 1][x].highlight = "move";
                }

                if (y - 1 == 0 && !newGrid[y - 1][x].piece) newGrid[y - 1][x].highlight = "recruite";

                if (y - 1 >= 0 && x - 1 >= 0 && newGrid[y - 1][x - 1].piece && isLower(newGrid[y - 1][x - 1].piece!))
                    newGrid[y - 1][x - 1].highlight = y - 1 == 0 ? "recruite": "capture";

                if (y - 1 >= 0 && x + 1 < 8 && newGrid[y - 1][x + 1].piece && isLower(newGrid[y - 1][x + 1].piece!))
                    newGrid[y - 1][x + 1].highlight = y - 1 == 0 ? "recruite": "capture";
            }

            // Springerlogik
            else if (cell.piece.toLowerCase() === "n") {
                const knightMoves = [
                    [-2, -1], [-2, 1], [2, -1], [2, 1],
                    [-1, -2], [-1, 2], [1, -2], [1, 2]
                ];

                knightMoves.forEach(([dy, dx]) => {
                    const ny = y + dy;
                    const nx = x + dx;

                    if (ny >= 0 && ny < 8 && nx >= 0 && nx < 8) {
                        let target = newGrid[ny][nx];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                });
            }

            // Läufer / Turm / Dame / König (Sliding Pieces)
            else {
                const rstop: boolean[] = [false, false, false, false];
                const bstop: boolean[] = [false, false, false, false];

                for (let i = 1; i < 8; i++) {
                    // Gerade Linien (Turm / Dame / König)
                    if(cell.piece.toLowerCase() == "r" || cell.piece.toLowerCase() == "k" || cell.piece.toLowerCase() == "q"){
                        const dirs = [
                            [y + i, x], [y - i, x],
                            [y, x + i], [y, x - i]
                        ];

                        dirs.forEach(([ny, nx], idx) => {
                            if (!rstop[idx] && ny >= 0 && ny < 8 && nx >= 0 && nx < 8) {
                                const target = newGrid[ny][nx];
                                target.highlight = target.piece === null
                                    ? "move"
                                    : target.color !== cell.color
                                        ? "capture"
                                        : "neutral";
                                if (target.piece !== null) rstop[idx] = true;
                            }
                        });
                    }

                    // Diagonalen (Läufer / Dame / König)
                    if(cell.piece.toLowerCase() == "b" || cell.piece.toLowerCase() == "k" || cell.piece.toLowerCase() == "q"){
                        const diag = [
                            [y + i, x + i], [y + i, x - i],
                            [y - i, x + i], [y - i, x - i]
                        ];

                        diag.forEach(([ny, nx], idx) => {
                            if (!bstop[idx] && ny >= 0 && ny < 8 && nx >= 0 && nx < 8) {
                                const target = newGrid[ny][nx];
                                target.highlight = target.piece === null
                                    ? "move"
                                    : target.color !== cell.color
                                        ? "capture"
                                        : "neutral";
                                if (target.piece !== null) bstop[idx] = true;
                            }
                        });
                    }

                    // König darf nur 1 Feld
                    if(cell.piece.toLowerCase() == "k") break;
                }
            }

            return newGrid;
        });
    };

    // Zielzug setzen
    const MakeMove = (x: number, y: number, methode: string) => {
        setMove({
            methode: methode,
            piece: Move?.piece!,
            x: Move?.x!,
            y: Move?.y!,
            xnew: x,
            ynew: y,
            newpiece: "",
        });

        // Highlights resetten
        setGrid(prev =>
            prev.map(row => row.map(cell => ({ ...cell, highlight: "neutral" as Highlight })))
        );
    }

    // Zug an Server senden
    const SendMove = async (not_AI: boolean) => {
        try {
            setShowError(false);

            const response = await fetch(`http://localhost:5146/MakeMove?IsPVP=${IsPVP}`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
                body: JSON.stringify({
                    IsPVP: not_AI,
                    move: {
                        x: Move?.x,
                        y: Move?.y,
                        xnew: Move?.xnew,
                        ynew: Move?.ynew,
                        new_piece: Move?.newpiece
                    }
                })
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok){
                setError(
                    response.status === 409 || response.status === 401
                        ? data.message || "Fehler"
                        : "Serverfehler – bitte erneut versuchen"
                );
                setShowError(true);
                return;
            };

            console.log(data.message);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    }

    return (
        <div className="flex-box">

            {/* Fehleranzeige */}
            {ShowError && <p style={{color: "red"}}>{Error}</p>}

            {/* Schachbrett */}
            <table>
                <tbody>
                    {grid.map((row, y) => (
                        <tr key={y}>

                            {/* Reihen-Index */}
                            <td style={{textAlign: "center", width: 20, height: 40}}>
                                {8 - y}
                            </td>

                            {/* Felder */}
                            {row.map((cell, x) => (
                                <td key={x}>
                                    <button
                                        style={{
                                            width: 40,
                                            height: 40,
                                            display: "flex",
                                            justifyContent: "center",
                                            alignItems: "center",

                                            // Highlight + Brettfarben
                                            backgroundColor:
                                                cell.highlight === "move" ? "blue" :
                                                cell.highlight === "capture" ? "green" :
                                                cell.highlight === "recruite" ? "orange" :
                                                (y % 2 == 0 && x % 2 == 0) || (y % 2 == 1 && x % 2 == 1)
                                                    ? "white"
                                                    : "black"
                                        }}

                                        // Klick-Logik
                                        onClick={() => {
                                            if (cell.highlight === "neutral" && !cell.piece) return;

                                            if (cell.highlight === "neutral") {
                                                ShowMoves(x, y);
                                            } else {
                                                MakeMove(x, y, cell.highlight);
                                            }
                                        }}
                                    >

                                    {/* Figuren-Rendering */}
                                    {cell.piece === "p" && <img src="/src/bp.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "P" && <img src="/src/wp.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "n" && <img src="/src/bn.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "N" && <img src="/src/wn.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "r" && <img src="/src/br.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "R" && <img src="/src/wr.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "b" && <img src="/src/bb.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "B" && <img src="/src/wb.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "q" && <img src="/src/bq.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "Q" && <img src="/src/wq.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "k" && <img src="/src/bk.png" style={{ width: 40, height: 40 }} />}
                                    {cell.piece === "K" && <img src="/src/wk.png" style={{ width: 40, height: 40 }} />}

                                    </button>
                                </td>
                            ))}
                        </tr>
                    ))}

                    {/* Spalten-Beschriftung */}
                    <tr>
                        <td></td>
                        {Array.from({ length: 8 }, (_, i) => (
                            <td style={{textAlign: "center"}} key={i}>
                                {String.fromCharCode("A".charCodeAt(0) + i)}
                            </td>
                        ))}
                    </tr>

                </tbody>
            </table>

            {/* Move-Box nur wenn Spieler dran ist */}
            {myturn && <div className="box">

                {/* Anzeige des aktuellen Zuges */}
                <div>
                    <p>
                        Ziehe {(Move?.piece || "-") + " "} 
                        von {(Move?.y !== undefined ? String.fromCharCode("A".charCodeAt(0) + (Move?.x || 0)) : "-") + "/"}
                        {(8 - Move?.y! || "-") + " "}
                        nach {(Move?.ynew !== undefined && Move?.ynew >= 0 ? String.fromCharCode("A".charCodeAt(0) + (Move?.xnew || 0)) : "-") + "/"} 
                        {(Move?.ynew !== undefined && Move?.ynew >= 0 ? 8 - Move?.ynew : "-")}
                    </p>
                </div >

                {/* Promotion Auswahl */}
                {Move?.methode === "recruite" && 
                <div className="flex-box-horizontal">
                    und rekrutiere 
                    <select
                        value={Move?.newpiece || ""}
                        onChange={(e) => {
                            if (Move) {
                                setMove({ ...Move, newpiece: e.target.value });
                            }
                        }}
                    >
                        <option value="">--Wähle Figur--</option>
                        <option value="Q">Dame</option>
                        <option value="R">Turm</option>
                        <option value="B">Läufer</option>
                        <option value="N">Springer</option>
                    </select>
                </div>}

                {/* Senden-Button */}
                <button 
                    disabled={
                        !Move ||
                        Move.y === undefined ||
                        Move.ynew === undefined ||
                        Move.ynew < 0 ||
                        (Move.methode === "recruite" && Move.newpiece === "")
                    }
                    onClick={async () => {
                        await SendMove(true);
                        if(!IsPVP)
                        {
                            await SendMove(false);
                        }
                    }}
                >
                    Senden
                </button>

            </div>}
        </div>
    );
}
