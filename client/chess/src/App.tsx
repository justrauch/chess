import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "./App.css";

export default function App() {

  const navigate = useNavigate();

  /* ---------------- View State ---------------- */
  const [showSignup, setShowSignup] = useState(true);
  const [showLogin, setShowLogin] = useState(false);

  /* ---------------- Error Handling ---------------- */
  const [showError, setShowError] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  /* ---------------- Signup Form State ---------------- */
  const [signupUsername, setSignupUsername] = useState("");
  const [signupPassword, setSignupPassword] = useState("");
  const [signupPasswordRepeat, setSignupPasswordRepeat] = useState("");

  /* ---------------- Login Form State ---------------- */
  const [loginUsername, setLoginUsername] = useState("");
  const [loginPassword, setLoginPassword] = useState("");

  const sendAuthRequest = async (
    username: string,
    password: string,
    endpoint: "signup" | "login"
  ) => {
    try {
      setShowError(false);

      const response = await fetch(`http://localhost:5146/${endpoint}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ Username: username, Password: password }),
      });
      
      let data: any = {};
      try { data = await response.json(); } catch {}

      if (!response.ok) {
        setErrorMessage(
          response.status === 409 || response.status === 401
            ? data.detail || "Fehler"
            : "Serverfehler – bitte erneut versuchen"
        );
        setShowError(true);
        return;
      }

      if (endpoint === "login") {
        navigate("/home");
      }

      if (endpoint === "signup") {
        setShowLogin(true);
        setShowSignup(false);
      }

    } catch (error) {
      console.error(`Authentifizierungsanfrage fehlgeschlagen (${endpoint}):`, error);
    }
  };

  return (
    <>
    <div className="outer-div">
      {/* ---------------- Navigation ---------------- */}
      {(showSignup || showLogin) && (
        <div className="div-buttons">
          <button
            name="btn-switch-login"
            onClick={() => {
              setShowLogin(true);
              setShowSignup(false);
            }}
          >
            Einloggen
          </button>

          <button
            name="btn-switch-signup"
            onClick={() => {
              setShowLogin(false);
              setShowSignup(true);
            }}
          >
            Registrieren
          </button>
        </div>
      )}

      <div>
        {/* ---------------- Fehlermeldung ---------------- */}
        {showError && (
          <span style={{ color: "red" }} data-testid="error-message">
            {errorMessage}
          </span>
        )}

        {/* ---------------- Registrierungsformular ---------------- */}
        {showSignup && (
          <form data-testid="form-signup">
            <div className="form-row">
              <label htmlFor="signup-username">Name:</label>
              <input
                id="signup-username"
                name="signup-username"
                type="text"
                maxLength={50}
                onInput={e => setSignupUsername((e.target as HTMLInputElement).value || "")}
              />
            </div>

            <div className="form-row">
              <label htmlFor="signup-password">Passwort:</label>
              <input
                id="signup-password"
                name="signup-password"
                type="password"
                onInput={e => setSignupPassword((e.target as HTMLInputElement).value || "")}
              />
            </div>

            {signupPassword !== signupPasswordRepeat && (
              <span style={{ color: "red" }} data-testid="signup-password-mismatch">
                Passwort stimmt nicht überein!
              </span>
            )}

            <div className="form-row">
              <label htmlFor="signup-password-repeat">Passwort wiederholen:</label>
              <input
                id="signup-password-repeat"
                name="signup-password-repeat"
                type="password"
                onInput={e => setSignupPasswordRepeat((e.target as HTMLInputElement).value || "")}
              />
            </div>

            <button
              type="button"
              name="btn-signup-submit"
              className="myButton"
              disabled={
                signupUsername.trim() === "" ||
                signupPassword.trim() === "" ||
                signupPassword !== signupPasswordRepeat
              }
              onClick={e => {
                e.preventDefault();
                sendAuthRequest(signupUsername, signupPassword, "signup");
              }}
            >
              Absenden
            </button>
          </form>
        )}

        {/* ---------------- Loginformular ---------------- */}
        {showLogin && (
          <form data-testid="login-form">
            <div className="form-row">
              <label htmlFor="login-username">Name:</label>
              <input
                id="login-username"
                name="login-username"
                type="text"
                maxLength={50}
                onInput={e => setLoginUsername((e.target as HTMLInputElement).value || "")}
              />
            </div>

            <div className="form-row">
              <label htmlFor="login-password">Passwort:</label>
              <input
                id="login-password"
                name="login-password"
                type="password"
                onInput={e => setLoginPassword((e.target as HTMLInputElement).value || "")}
              />
            </div>

            <button
              type="button"
              name="btn-login-submit"
              id = "btn-login-submit"
              className="myButton"
              disabled={loginUsername.trim() === "" || loginPassword.trim() === ""}
              onClick={e => {
                e.preventDefault();
                sendAuthRequest(loginUsername, loginPassword, "login");
              }}
            >
              Absenden
            </button>
          </form>
        )}
      </div>
      </div>
    </>
  );
}

