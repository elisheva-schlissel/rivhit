import type { AttendanceRecord } from "../types";
import { formatZurich, formatDuration } from "../util";

// The punch history table for the selected employee.
export function RecordsTable({ records }: { records: AttendanceRecord[] }) {
  if (records.length === 0) {
    return <p className="muted">עדיין אין רשומות נוכחות.</p>;
  }

  return (
    <table className="records">
      <thead>
        <tr>
          <th>כניסה (ציריך)</th>
          <th>יציאה (ציריך)</th>
          <th>משך</th>
          <th>מקור</th>
        </tr>
      </thead>
      <tbody>
        {records.map((r) => (
          <tr key={r.id} className={r.isOpen ? "open-row" : ""}>
            <td>{formatZurich(r.clockInZurich)}</td>
            <td>
              {r.isOpen ? <span className="badge badge-open">בתהליך</span> : formatZurich(r.clockOutZurich)}
            </td>
            <td>{formatDuration(r.durationMinutes)}</td>
            <td>
              <span className="src">{r.clockInSource}</span>
              {(r.clockInIsFallback || r.clockOutIsFallback) && (
                <span className="badge badge-warn" title="הזמן התקבל ממנגנון fallback מבוסס offset שמור">
                  fallback
                </span>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
