import { useNavigate } from "react-router-dom";
import { useState, useEffect } from "react";
import "./App.css";

import Chessboard from "./Chessboard";

type Match = {
    Winner: string,
    Loser: string,
    Anz_Moves: string
};

export default function App() {

    const navigate = useNavigate();

    const [ShowButtons, setShowButtons] = useState(true);
    const [ShowPVP, setPVP] = useState(false);
    const [ShowMatchHistory, setShowMatchHistory] = useState(false);
    const [ShowPVE, setPVE] = useState(false);
    const [ShowMatch, setShowMatch] = useState(false);
    const [MyTurn, setMyTurn] = useState(false);
    const [Color, setColor] = useState("");
    const [Status, setStatus] = useState("");
    const [Message, setMessage] = useState("");
    const [ShowMessage, setShowMessage] = useState(false);
    const [Board, setBoard] = useState("");
    const [Winner, setWinner] = useState("");
    const [MatchHistory, setMatchHistory] = useState<Match[]>([]);


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

    const handleMatch = async () => {
        try {
            const response = await fetch(`http://localhost:5146/searchMatch`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });
            
            try {await response.json();} catch {}

            if (!response.ok) {
                return;
            }

            handlegetQueueState();

            } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    const handlegetGameState = async () => {
        try {
            const response = await fetch(`http://localhost:5146/getGameState`, {
                method: "GET",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });
            
            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) {
                return;
            }

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

    const handlegetQueueState = async () => {
        try {
            const response = await fetch(`http://localhost:5146/getQueueState`, {
                method: "GET",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            if (data.message === "Match existiert bereits!") {
                handlegetGameState();
                setShowMessage(false);
            }
            else if (data.message === "Noch nicht gesucht!") {
                setShowMatch(false);
            }
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

    const handleleaveMatch = async () => {
        try {
            const response = await fetch(`http://localhost:5146/leaveMatch`, {
                method: "Post",
                headers: { "Content-Type": "application/json" },
                credentials: "include",
            });

            let data: any = {};
            try { data = await response.json(); } catch {}

            if (!response.ok) return;

            console.log(data.message);
            setShowMatch(false); setShowButtons(true); setPVP(false); setPVE(false);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

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

    useEffect(() => {
        handlegetQueueState();

        const interval = setInterval(() => {
            handlegetQueueState();
        }, 5000);

        return () => clearInterval(interval);
    }, []);

    return (
        <div className="outer-div">
            <div className="div-buttons">
                {!ShowButtons && <button onClick={() => {setShowMatch(false); setShowButtons(true); setPVP(false); setPVE(false);}}>Zurück</button>}
                {ShowPVP && ShowMatch && <button onClick={handleleaveMatch}>{Winner != "none yet" ? "Beenden" : "Aufgeben"}</button>}
                <button onClick={logout}>Abmelden</button>
            </div>
            {ShowButtons &&             
            <div className="div-buttons">
                <button onClick={() => {handlegetQueueState(); setShowButtons(false); setPVP(true); setPVE(false);}}>PVP</button>
                <button onClick={() => {setShowButtons(false); setPVP(false); setPVE(true);}}>PVE</button>
            </div>}
            {ShowPVP && 
            <div className="div-buttons">
                <button onClick={() => {setShowMatchHistory(false);}}>Spiel</button>
                <button onClick={() => {handlegetMatchHistory(); setShowMatchHistory(true);}}>Spiel Verlauf</button>
            </div>}
            {ShowPVP && !ShowMatchHistory &&
            <div>
                {!ShowMatch && !ShowMessage && <button onClick={handleMatch}>Match suchen</button>}
                {!ShowMatch && ShowMessage && <p>{Message}</p>}
                {ShowMatch && <div>
                    <p style={{ textAlign: "center" }}>
                        Status: {Status} <br />
                        Color: {Color} <br />
                        {Winner != "none yet" ? "Winner: " + Winner : ""}
                    </p>
                    <Chessboard board = {Board} mycolor = {Color} myturn = {MyTurn}></Chessboard>
                </div>}
            </div>}
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