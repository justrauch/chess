import { useNavigate } from "react-router-dom";
import { useState, useEffect, useRef } from "react";
import "./App.css";

import Chessboard from "./Chessboard";

// Match-Datentyp für Match-Historie
type Match = {
    Winner: string,
    Loser: string,
    Anz_Moves: string
};

export default function App() {

    const navigate = useNavigate();

    // UI-State-Flags
    const [ShowButtons, setShowButtons] = useState(true);
    const [ShowPVP, setPVP] = useState(false);
    const [ShowMatchHistory, setShowMatchHistory] = useState(false);
    const [ShowPVE, setPVE] = useState(false);
    const [ShowMatch, setShowMatch] = useState(false);

    // Game-State
    const [MyTurn, setMyTurn] = useState(false);
    const [Color, setColor] = useState("");
    const [Status, setStatus] = useState("");
    const [Message, setMessage] = useState("");
    const [ShowMessage, setShowMessage] = useState(false);
    const [Board, setBoard] = useState("");
    const [Winner, setWinner] = useState("");

    // Match-Historie
    const [MatchHistory, setMatchHistory] = useState<Match[]>([]);

    // Logout-Request
    const logout = async () => {
        try {
            const response = await fetch(`http://localhost:5146/logout`, {
                method: "POST",
                credentials: "include"
            });

            if (!response.ok) { 
                await response.json(); 
                console.error(`Fehler beim Logout`);
                navigate("/");
                return; 
            }

            await response.json();
            navigate("/");

        } catch (error) {
            console.error(`Fehler beim Logout:`, error);
        }
    };

    // Match suchen
    const handleMatch = async () => {
        try {
            const response = await fetch(`http://localhost:5146/searchMatch?IsPVP=${ShowPVP}`, {
                method: "POST",
                credentials: "include",
            });
            
            try { await response.json(); } catch {}

            if (!response.ok) return;

            // Queue-Status abrufen
            handlegetQueueState(pvpRef.current);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    // Aktuellen Spielstatus abrufen
    const handlegetGameState = async (IsPVP: boolean) => {
        try {
            const response = await fetch(`http://localhost:5146/getGameState?IsPVP=${IsPVP}`, {
                method: "GET",
                credentials: "include",
            });
            
            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            // Game-State setzen
            setShowMatch(true);
            setMyTurn(data.your_turn == "true");
            setBoard(data.game_state);
            setColor(data.your_color);
            setStatus(data.game_status);
            setWinner(data.winner);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    // Queue-Status abrufen
    const handlegetQueueState = async (IsPVP: boolean) => {
        try {
            const response = await fetch(`http://localhost:5146/getQueueState?IsPVP=${IsPVP}`, {
                method: "GET",
                credentials: "include",
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            // Match existiert bereits → Spiel starten
            if (data.message === "Match existiert bereits!") {
                handlegetGameState(pvpRef.current);
                setShowMessage(false);
            }
            // Noch kein Match gesucht
            else if (data.message === "Noch nicht gesucht!") {
                setShowMatch(false);
            }
            // Matchmaking läuft
            else if (data.message === "Spieler wird gesucht!"){
                setShowMatch(false);
                setShowMessage(true);
                setMessage("Suche läuft...");
            }

            console.log(data.message);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    // Match verlassen / aufgeben
    const handleleaveMatch = async (IsPVP: boolean) => {
        try {
            const response = await fetch(`http://localhost:5146/leaveMatch?IsPVP=${IsPVP}`, {
                method: "Post",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            console.log(data.message);

            // UI resetten
            setShowMatch(false); 
            setShowButtons(true); 
            setPVP(false); 
            setPVE(false);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    // Match-Historie laden
    const handlegetMatchHistory = async () => {
        try {
            const response = await fetch(`http://localhost:5146/getallMatches/user`, {
                method: "Get",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            const matches: Match[] = JSON.parse(data.message);
            setMatchHistory(matches);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    // Ref für stabilen PVP-State im Polling
    const pvpRef = useRef(ShowPVP);

    useEffect(() => {
        pvpRef.current = ShowPVP;
    }, [ShowPVP]);

    // Polling für Queue-Status
    useEffect(() => {
        handlegetQueueState(pvpRef.current);

        const interval = setInterval(() => {
            handlegetQueueState(pvpRef.current);
        }, 2500);

        return () => clearInterval(interval);
    }, []);

    return (
        <div className="outer-div">
            {/* Top-Bar Buttons: Zurück / Aufgeben / Logout */}
            <div className="div-buttons">
                {!ShowButtons && 
                    <button onClick={() => {
                        setShowMatch(false); 
                        setShowButtons(true); 
                        setPVP(false); 
                        setPVE(false);
                    }}>
                        Zurück
                    </button>
                }

                {/* Match beenden oder aufgeben */}
                {(ShowPVP || ShowPVE) && ShowMatch && 
                    <button onClick={() => handleleaveMatch(pvpRef.current)}>
                        {Winner != "none yet" ? "Beenden" : "Aufgeben"}
                    </button>
                }

                {/* Logout */}
                <button onClick={logout}>Abmelden</button>
            </div>

            {/* Start-Menü: Auswahl PVP / PVE */}
            {ShowButtons &&             
            <div className="div-buttons">
                <button onClick={() => {
                    handlegetQueueState(pvpRef.current); 
                    setShowButtons(false); 
                    setPVP(true); 
                    setPVE(false);
                }}>
                    PVP
                </button>

                <button onClick={() => {
                    handlegetQueueState(pvpRef.current); 
                    setShowButtons(false); 
                    setPVP(false); 
                    setPVE(true);
                }}>
                    PVE
                </button>
            </div>}

            {/* PVP-Menü: Spiel oder Verlauf */}
            {ShowPVP && 
            <div className="div-buttons">
                <button onClick={() => {setShowMatchHistory(false);}}>
                    Spiel
                </button>

                <button onClick={() => {
                    handlegetMatchHistory(); 
                    setShowMatchHistory(true);
                }}>
                    Spiel Verlauf
                </button>
            </div>}

            {/* Match-Suche & Spielanzeige */}
            {(ShowPVP || ShowPVE) && !ShowMatchHistory &&
            <div>
                {/* Match suchen */}
                {!ShowMatch && !ShowMessage && 
                    <button onClick={handleMatch}>Match suchen</button>
                }

                {/* Matchmaking-Status */}
                {!ShowMatch && ShowMessage && 
                    <p>{Message}</p>
                }

                {/* Aktives Spiel */}
                {ShowMatch && 
                <div>
                    <p style={{ textAlign: "center" }}>
                        Status: {Status} <br />
                        Color: {Color} <br />
                        {Winner != "none yet" ? "Winner: " + Winner : ""}
                    </p>

                    {/* Chessboard-Komponente */}
                    <Chessboard 
                        board={Board} 
                        mycolor={Color} 
                        myturn={MyTurn} 
                        IsPVP={ShowPVP}
                    />
                </div>}
            </div>}

            {/* Match-Historie Tabelle */}
            {ShowPVP && ShowMatchHistory &&
                <div className="table-container">
                    <table>
                        <thead>
                            <tr>
                                <th>Gewinner</th>
                                <th>Verlierer</th>
                                <th>Anzahl Züge</th>
                            </tr>
                        </thead>

                        <tbody>
                            {MatchHistory.map((match) => (
                                <tr>
                                    <td style={{textAlign:"center"}}>{match.Winner}</td>
                                    <td style={{textAlign:"center"}}>{match.Loser}</td>
                                    <td style={{textAlign:"center"}}>{match.Anz_Moves}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            }
        </div>
    )
}
