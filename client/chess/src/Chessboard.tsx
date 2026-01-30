import "./App.css";
import { useState, useEffect } from "react";

export default function Chessboard({ board }: { board: string }) {

    const [grid, setGrid] = useState<string[][]>([]);

    useEffect(() => {
        const rows = board.split("/");
        const newGrid: string[][] = [];

        rows.forEach(row => {
        const rowArray: string[] = [];
        
        row.split("").forEach(char => {
            if (!isNaN(Number(char))) {
            const emptyCount = Number(char);
            for (let i = 0; i < emptyCount; i++) {
                rowArray.push("-");
            }
            } else {
            rowArray.push(char);
            }
        });

        newGrid.push(rowArray);
        });

        setGrid(newGrid);
    }, [board]);

    return (
        <div>
            <table>
                <tbody>
                    {grid.map(g => (
                        <tr>
                            {g.map(gl => (
                                <td>
                                    {gl}
                                </td>
                            ))}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    )
}