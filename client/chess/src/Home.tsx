import { useNavigate } from "react-router-dom";
import "./App.css";

export default function App() {

    const navigate = useNavigate();

    const logout = async () => {
        try {
        const response = await fetch(`http://localhost:5146/logout`, {
            method: "POST",
            credentials: "include"
        });

        if (!response.ok) { 
            const data = await response.json(); 
            console.error(`Fehler beim Logout`);
            return; 
        }

        await response.json();
        navigate("/");

        } catch (error) {
        console.error(`Fehler beim Logout:`, error);
        }
    };

    return (
        <div className="outer-div">
            <button onClick={logout}>Abmelden</button>
        </div>
    )
}