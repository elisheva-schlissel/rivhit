// Helper functions for formatting dates and durations. All display is done in the Europe/Zurich
// time zone, so that the displayed time is not reinterpreted according to the browser's time zone.

/** Formats an ISO date-time (with offset) as Zurich time, e.g. "16 ביולי 2026, 19:17:48". */
export function formatZurich(iso: string | null | undefined): string {
  if (!iso) return "—";
  // The string already carries Zurich's offset; we display it in this time zone via Intl
  // so that we never reinterpret it according to the browser's local time zone.
  return new Intl.DateTimeFormat("he-IL", {
    timeZone: "Europe/Zurich",
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(new Date(iso));
}

/** Formats a duration given in minutes (including a fraction) as "Hh Mm". */
export function formatDuration(totalMinutes: number | null | undefined): string {
  if (totalMinutes == null) return "—";
  const mins = Math.max(0, Math.round(totalMinutes));
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  if (h === 0) return `${m} דק׳`;
  return `${h} שע׳ ${String(m).padStart(2, "0")} דק׳`;
}

/** Formats a live Date as Zurich time HH:mm:ss for the header indicator. */
export function formatZurichTime(d: Date): string {
  return new Intl.DateTimeFormat("he-IL", {
    timeZone: "Europe/Zurich",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(d);
}

/** Formats the full date in Hebrew (day of week, day, month, year). */
export function formatZurichDate(d: Date): string {
  return new Intl.DateTimeFormat("he-IL", {
    timeZone: "Europe/Zurich",
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
  }).format(d);
}

/** Formats only the time (HH:mm:ss) of an ISO moment in the Zurich zone — for use in status messages. */
export function formatZurichClock(iso: string): string {
  return new Intl.DateTimeFormat("he-IL", {
    timeZone: "Europe/Zurich",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(new Date(iso));
}
