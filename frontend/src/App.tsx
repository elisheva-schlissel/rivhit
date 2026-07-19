import { useCallback, useEffect, useState } from "react";
import { api, ApiCallError } from "./api";
import type { Employee, Status, AttendanceRecord } from "./types";
import { useServerClock } from "./useServerClock";
import { formatZurichTime, formatZurichDate, formatDuration, formatZurichClock } from "./util";
import { RecordsTable } from "./components/RecordsTable";

// The main component: employee selector, live clock indicator, punch status, Clock In/Out buttons, and history.
export default function App() {
  const clock = useServerClock();

  const [employees, setEmployees] = useState<Employee[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [status, setStatus] = useState<Status | null>(null);
  const [records, setRecords] = useState<AttendanceRecord[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // Load the employee list once.
  useEffect(() => {
    api.getEmployees()
      .then((list) => {
        setEmployees(list);
        if (list.length > 0) setSelectedId(list[0].id);
      })
      .catch((e) => setError(errMsg(e)));
  }, []);

  // Refresh status + history for a given employee.
  const refresh = useCallback(async (employeeId: number) => {
    const [st, recs] = await Promise.all([
      api.getStatus(employeeId),
      api.getRecords(employeeId),
    ]);
    setStatus(st);
    setRecords(recs);
  }, []);

  // Whenever the selected employee changes, reload status and history.
  useEffect(() => {
    if (selectedId == null) return;
    setError(null);
    setNotice(null);
    refresh(selectedId).catch((e) => setError(errMsg(e)));
  }, [selectedId, refresh]);

  // Handles a Clock In/Out click: calls the API, shows a message, and refreshes.
  async function handlePunch(kind: "in" | "out") {
    if (selectedId == null || busy) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const result = kind === "in" ? await api.clockIn(selectedId) : await api.clockOut(selectedId);
      const t = result.time;
      const verb = kind === "in" ? "כניסה הוחתמה" : "יציאה הוחתמה";
      setNotice(
        `${verb} בשעה ${formatZurichClock(t.zurich)} (ציריך, דרך ${t.source})` +
          (t.isFallback ? " — זמן fallback" : "")
      );
      await refresh(selectedId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setBusy(false);
    }
  }

  const selectedEmployee = employees.find((e) => e.id === selectedId) ?? null;
  const clockedIn = status?.isClockedIn ?? false;

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="logo">🕐</span>
          <div>
            <h1>שעון נוכחות</h1>
            <span className="sub">אזור הזמן הקובע · Europe/Zurich</span>
          </div>
        </div>
        <div className="clock">
          {clock.now ? (
            <>
              <div className="clock-time">{formatZurichTime(clock.now)}</div>
              <div className="clock-date">{formatZurichDate(clock.now)}</div>
              <div className="clock-src">
                מקור: {clock.meta?.source ?? "…"}
                {clock.meta?.isFallback && <span className="badge badge-warn">fallback</span>}
              </div>
            </>
          ) : (
            <div className="clock-time muted">{clock.error ? "הזמן אינו זמין" : "מסנכרן…"}</div>
          )}
        </div>
      </header>

      {clock.error && (
        <div className="banner banner-warn">
          לא ניתן היה להגיע למקור הזמן הקובע: {clock.error}. ייתכן שהחתמות יידחו עד שהמקור יתאושש.
        </div>
      )}

      <main className="card">
        <label className="field">
          <span>עובד</span>
          <select
            value={selectedId ?? ""}
            onChange={(e) => setSelectedId(Number(e.target.value))}
            disabled={employees.length === 0}
          >
            {employees.map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName} (#{e.employeeNumber})
              </option>
            ))}
          </select>
        </label>

        <div className={`status ${clockedIn ? "status-in" : "status-out"}`}>
          {status ? (
            clockedIn ? (
              <>
                <strong>{selectedEmployee?.fullName}</strong> <em>מוחתם/ת ככניסה</em>
                <div className="status-detail">
                  מאז {status.clockedInSince ? formatZurichClock(status.clockedInSince) : "—"}
                  {" · "}זמן שחלף {formatDuration(status.openDurationMinutes)}
                </div>
              </>
            ) : (
              <>
                <strong>{selectedEmployee?.fullName}</strong> <em>מוחתם/ת כיציאה</em>
              </>
            )
          ) : (
            <span className="muted">טוען מצב…</span>
          )}
        </div>

        <div className="actions">
          <button
            className="btn btn-in"
            disabled={busy || clockedIn || selectedId == null}
            onClick={() => handlePunch("in")}
          >
            כניסה (Clock In)
          </button>
          <button
            className="btn btn-out"
            disabled={busy || !clockedIn || selectedId == null}
            onClick={() => handlePunch("out")}
          >
            יציאה (Clock Out)
          </button>
        </div>

        {notice && <div className="banner banner-ok">{notice}</div>}
        {error && <div className="banner banner-err">{error}</div>}
      </main>

      <section className="card">
        <h2>פעילות אחרונה</h2>
        <RecordsTable records={records} />
      </section>

      <footer className="foot">
        השעות נלכדות משירות זמן חיצוני ונשמרות ב‑UTC; כל התצוגה היא באזור Europe/Zurich.
        שעון הדפדפן ושעון השרת אינם משמשים לעולם לרישום הנוכחות.
      </footer>
    </div>
  );
}

// Converts any error into a readable text message for the user.
function errMsg(e: unknown): string {
  if (e instanceof ApiCallError) return e.message;
  if (e instanceof Error) return e.message;
  return "משהו השתבש.";
}
