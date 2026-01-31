import { useNavigate } from "react-router-dom";
import { useState, useEffect } from "react";
import "./App.css";

import Chessboard from "./Chessboard";

export default function App() {

    const navigate = useNavigate();

    const [ShowButtons, setShowButtons] = useState(true);
    const [ShowPVP, setPVP] = useState(false);
    const [ShowPVE, setPVE] = useState(false);
    const [ShowMatch, setShowMatch] = useState(false);
    const [MyTurn, setMyTurn] = useState(false);
    const [Color, setColor] = useState("");
    const [Message, setMessage] = useState("");
    const [Board, setBoard] = useState("");
    const [interplayersearch, setinterplayersearch] = useState<number | null>(null);

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

            setShowMatch(true);
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
            setColor(data.your_color)

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
                setMessage("");
            }
            else if (data.message === "Noch nicht gesucht!") {
                setShowMatch(false);
            }
            else if (data.message === "Spieler wird gesucht!"){
                setShowMatch(true);
                setMessage("Suche läuft...");
            }

            console.log(data.message);

        } catch (error) {
            console.error(`Error in handle Match`, error);
        }
    };

    useEffect(() => {
        handlegetQueueState();
        if (!interplayersearch) {
            const id = setInterval(() => {
                handlegetQueueState();
                handlegetGameState();
            }, 10000);

            setinterplayersearch(id);
        }
    }, []);

    return (
        <div className="outer-div">
            <div className="div-buttons">
                {!ShowButtons && <button onClick={() => {setShowButtons(true); setPVP(false); setPVE(false);}}>Zurück</button>}
                <button onClick={logout}>Abmelden</button>
            </div>
            {ShowButtons &&             
            <div className="div-buttons">
                <button onClick={() => {setShowButtons(false); setPVP(true); setPVE(false);}}>PVP</button>
                <button onClick={() => {setShowButtons(false); setPVP(false); setPVE(true);}}>PVE</button>
            </div>}
            {ShowPVP && 
            <div>
                {!ShowMatch && <button onClick={handleMatch}>Match suchen</button>}
                {ShowMatch && <div>
                    <Chessboard board = {Board} mycolor = {Color} myturn = {MyTurn}></Chessboard>
                </div>}
            </div>}
        </div>
    )
}