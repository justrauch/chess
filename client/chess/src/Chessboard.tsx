import "./App.css";
import { useState, useEffect } from "react";

type Highlight = "neutral" | "move" | "capture" | "recruite";

type Cell = {
    piece: string | null;
    color: "white" | "black" | null;
    highlight: Highlight;
};

type ChessboardProps = {
    board: string;
    mycolor: string;
    myturn: boolean;
};

type Move = {
    methode: string;
    piece: string;
    x: number;
    y: number;
    xnew: number;
    ynew: number;
    newpiece: string;
};

export default function Chessboard({ board, mycolor, myturn }: ChessboardProps) {
    const [grid, setGrid] = useState<Cell[][]>([]);
    const [Move, setMove] = useState<Move>();

    const [Error, setError] = useState("");
    const [ShowError, setShowError] = useState(false);

    // Hilfsfunktionen
    const isUpper = (c: string) => c === c.toUpperCase() && c !== c.toLowerCase();
    const isLower = (c: string) => c === c.toLowerCase() && c !== c.toUpperCase();

    // Parse FEN in grid
    useEffect(() => {
        const rows = board.split("/");
        const newGrid: Cell[][] = [];

        rows.forEach(row => {
            const rowArray: Cell[] = [];

            row.split("").forEach(char => {
                if (!isNaN(Number(char))) {
                    const emptyCount = Number(char);
                    for (let i = 0; i < emptyCount; i++) {
                        rowArray.push({ piece: null, color: null, highlight: "neutral" });
                    }
                } else {
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

    const ShowMoves = (x: number, y: number) => {
        setGrid(prev =>
            prev.map(row => row.map(cell => ({ ...cell, highlight: "neutral" as Highlight })))
        );

        setGrid(prev => {
            const newGrid = prev.map(row => row.map(cell => ({ ...cell })));
            const cell = newGrid[y][x];

            setMove({
                methode: "",
                piece: cell.piece!,
                x: x,
                y: y,
                xnew: -1,
                ynew: -1,
                newpiece: ""
            });

            if (
                !cell.piece || 
                !myturn ||
                (
                    (mycolor === "white" && isLower(cell.piece || "")) || 
                    (mycolor === "black" && isUpper(cell.piece || ""))
                )
            ) return newGrid;

            if (cell.piece === "p") {
                // vor
                if (y + 1 < 8 && !newGrid[y + 1][x].piece) {
                    //erster zug
                    if (y == 1 && !newGrid[y + 2][x].piece) newGrid[y + 2][x].highlight = "move";
                    
                    newGrid[y + 1][x].highlight = "move";
                }

                if (y + 1 == 7 && !newGrid[y + 1][x].piece) newGrid[y + 1][x].highlight = "recruite";

                //links
                if (
                    y + 1 < 8 &&
                    x - 1 >= 0 &&
                    newGrid[y + 1][x - 1].piece &&
                    isUpper(newGrid[y + 1][x - 1].piece!)
                )
                    newGrid[y + 1][x - 1].highlight = "capture";
                    if (y + 1 == 7 && !newGrid[y + 1][x].piece) newGrid[y + 1][x - 1].highlight = "recruite";

                //rechts
                if (
                    y + 1 < 8 &&
                    x + 1 < 8 &&
                    newGrid[y + 1][x + 1].piece &&
                    isUpper(newGrid[y + 1][x + 1].piece!)
                )
                    newGrid[y + 1][x + 1].highlight = "capture";
                    if (y + 1 == 7 && !newGrid[y + 1][x].piece) newGrid[y + 1][x + 1].highlight = "recruite";
            }

            else if (cell.piece === "P") {
                //vor
                if (y - 1 > 0 && !newGrid[y - 1][x].piece) {
                    //erster zug
                    if (y == 6 && !newGrid[y - 2][x].piece) newGrid[y - 2][x].highlight = "move";

                    newGrid[y - 1][x].highlight = "move";
                }

                // vor auf letztes Feld
                if (y - 1 == 0 && !newGrid[y - 1][x].piece) newGrid[y - 1][x].highlight = "recruite";

                //links
                if (
                    y - 1 >= 0 &&
                    x - 1 >= 0 &&
                    newGrid[y - 1][x - 1].piece &&
                    isLower(newGrid[y - 1][x - 1].piece!)
                )
                    newGrid[y - 1][x - 1].highlight = "capture";
                    if (y - 1 == 0 && !newGrid[y - 1][x].piece) newGrid[y - 1][x - 1].highlight = "recruite";

                //rechts
                if (
                    y - 1 >= 0 &&
                    x + 1 < 8 &&
                    newGrid[y - 1][x + 1].piece &&
                    isLower(newGrid[y - 1][x + 1].piece!)
                )
                    newGrid[y - 1][x + 1].highlight = "capture";
                    if (y - 1 == 0 && !newGrid[y - 1][x].piece) newGrid[y - 1][x + 1].highlight = "recruite";
            }
            
            else if (cell.piece.toLowerCase() === "n") {
                if (y - 2 >= 0) {
                    if(x + 1 < 8)
                    {
                        let target = newGrid[y - 2 ][x + 1];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                    if(x - 1 >= 0)
                    {
                        let target = newGrid[y - 2][x - 1];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                }

                if (y + 2 < 8) {
                    if(x + 1 < 8)
                    {
                    let target = newGrid[y + 2][x + 1];
                    target.highlight = target.piece === null
                        ? "move"
                        : target.color !== cell.color
                            ? "capture"
                            : "neutral";
                    }
                    if(x - 1 >= 0)
                    {
                        let target = newGrid[y + 2][x - 1];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                }

                if (x - 2 >= 0) {
                    if(y + 1 < 8)
                    {
                        let target = newGrid[y + 1][x - 2];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                    if(y - 1 >= 0)
                    {
                        let target = newGrid[y - 1][x - 2];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                }

                if (x + 2 < 8) {
                    if(y + 1 < 8)
                    {
                        let target = newGrid[y + 1][x + 2];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                    if(y - 1 >= 0)
                    {
                        let target = newGrid[y - 1][x + 2];
                        target.highlight = target.piece === null
                            ? "move"
                            : target.color !== cell.color
                                ? "capture"
                                : "neutral";
                    }
                }
            }
            else{
                const rstop: boolean[] = [false, false, false, false];
                const bstop: boolean[] = [false, false, false, false];

                for (let i = 1; i < 8; i++) {
                    if(cell.piece.toLowerCase() == "r" || cell.piece.toLowerCase() == "k" || cell.piece.toLowerCase() == "q"){
                        // unten
                        if (!rstop[0] && y + i < 8) {
                            const target = newGrid[y + i][x];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) rstop[0] = true;
                        }

                        // oben
                        if (!rstop[1] && y - i >= 0) {
                            const target = newGrid[y - i][x];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) rstop[1] = true;
                        }

                        // rechts
                        if (!rstop[2] && x + i < 8) {
                            const target = newGrid[y][x + i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) rstop[2] = true;
                        }

                        // links
                        if (!rstop[3] && x - i >= 0) {
                            const target = newGrid[y][x - i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) rstop[3] = true;
                        }
                    }
                    if(cell.piece.toLowerCase() == "b" || cell.piece.toLowerCase() == "k" || cell.piece.toLowerCase() == "q"){
                        //unten rechts
                        if (!bstop[0] && y + i < 8 && x + i < 8) {
                            const target = newGrid[y + i][x + i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) bstop[0] = true;
                        }

                        // unten links
                        if (!bstop[1] && y + i < 8 && x - i >= 0) {
                            const target = newGrid[y + i][x - i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) bstop[1] = true;
                        }

                        // oben rechts
                        if (!bstop[2] && y - i >= 0 && x + i < 8) {
                            const target = newGrid[y - i][x + i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) bstop[2] = true;
                        }

                        // oben links
                        if (!bstop[3] && y - i >= 0 && x - i >= 0) {
                            const target = newGrid[y - i][x - i];
                            target.highlight = target.piece === null
                                ? "move"
                                : target.color !== cell.color
                                    ? "capture"
                                    : "neutral";
                            if (target.piece !== null) bstop[3] = true;
                        }
                    }

                    if(cell.piece.toLowerCase() == "k" && i >= 1){
                        break;
                    }
                }
            }

            return newGrid;
        });
    };

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
        setGrid(prev =>
            prev.map(row => row.map(cell => ({ ...cell, highlight: "neutral" as Highlight })))
        );
    }

    const SendMove = async () => {
        try {
            setShowError(false);
            const response = await fetch(`http://localhost:5146/MakeMove`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
                body: JSON.stringify({ x:Move?.x, y:Move?.y, xnew:Move?.xnew, ynew:Move?.ynew, new_piece:Move?.newpiece }),
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
            {ShowError && <p style={{color: "red"}}>{Error}</p>}
            <table>
                <thead>
                </thead>
                <tbody>
                    {grid.map((row, y) => (
                        <tr key={y}>
                            <td style={{textAlign: "center", width: 20, height: 40,}}>{8 - y}</td>
                            {row.map((cell, x) => (
                                <td key={x}>
                                    <button
                                    style={{
                                        width: 40,
                                        height: 40,
                                        display: "flex",
                                        justifyContent: "center",
                                        alignItems: "center",
                                        backgroundColor:
                                        cell.highlight === "move"
                                            ? "blue"
                                            : cell.highlight === "capture"
                                            ? "green"
                                            : cell.highlight === "recruite"
                                            ? "orange"
                                            : (y % 2 == 0 && x % 2 == 0) || (y % 2 == 1 && x % 2 == 1)
                                            ? "white"
                                            : "black"
                                    }}
                                    onClick={() => {
                                        if (cell.highlight === "neutral" && !cell.piece) return;

                                        if (cell.highlight === "neutral") {
                                        ShowMoves(x, y);
                                        } else {
                                        MakeMove(x, y, cell.highlight);
                                        }
                                    }}
                                    >
                                    {cell.piece === "p" && (
                                        <img src="/src/bp.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "P" && (
                                        <img src="/src/wp.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "n" && (
                                        <img src="/src/bn.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "N" && (
                                        <img src="/src/wn.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "r" && (
                                        <img src="/src/br.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "R" && (
                                        <img src="/src/wr.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "b" && (
                                        <img src="/src/bb.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "B" && (
                                        <img src="/src/wb.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "q" && (
                                        <img src="/src/bq.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "Q" && (
                                        <img src="/src/wq.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "k" && (
                                        <img src="/src/bk.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    {cell.piece === "K" && (
                                        <img src="/src/wk.png" style={{ width: 40, height: 40 }} />
                                    )}
                                    </button>
                                </td>
                            ))}
                        </tr>
                    ))}
                    <tr>
                        <td></td>
                        {Array.from({ length: 8 }, (_, i) => (
                            <td style={{textAlign: "center"}} key={i}>{String.fromCharCode("A".charCodeAt(0) + i)}</td>
                        ))}
                    </tr>
                </tbody>
            </table>
            {myturn && <div className="box">
                <div>
                    <p>
                        Ziehe {(Move?.piece || "-") + " "} 
                        von {(Move?.y !== undefined ? 
                        String.fromCharCode("A".charCodeAt(0) + (Move?.x || 0)) : "-") + "/"}
                        {(8 - Move?.y! || "-") + " "}
                        nach {(Move?.ynew !== undefined && Move?.ynew >= 0 ? 
                        String.fromCharCode("A".charCodeAt(0) + (Move?.xnew || 0)) : "-") + "/"} 
                        {(Move?.ynew !== undefined && Move?.ynew >= 0 ? 8 - Move?.ynew : "-")}
                    </p>
                </div >
                {Move?.methode === "recruite" && <div className="flex-box-horizontal">
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
                <button disabled={
                        !Move ||
                        Move.y === undefined ||
                        Move.ynew === undefined ||
                        Move.ynew < 0 ||
                        (Move.methode === "recruite" && Move.newpiece === "")
                    }
                    onClick={SendMove}
                >
                    Senden
                </button>
            </div>}
        </div>
    );
}
