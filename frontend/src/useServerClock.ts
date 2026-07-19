import { useEffect, useRef, useState } from "react";
import { api } from "./api";
import type { TimeNow } from "./types";

export interface ServerClock {
  /** The best estimate of the current moment, anchored to the latest server read. */
  now: Date | null;
  /** Metadata about the latest authoritative read from the server. */
  meta: TimeNow | null;
  error: string | null;
}

/**
 * Manages a live clock indicator that is *anchored to the server's authoritative time*, not the
 * browser clock. We fetch the server time, remember the offset between it and the device clock, then
 * "tick" locally for smoothness and re-sync periodically. Attendance punches always use a fresh
 * server-side read on the server — this hook is for display only.
 */
export function useServerClock(resyncMs = 60_000): ServerClock {
  const [meta, setMeta] = useState<TimeNow | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [now, setNow] = useState<Date | null>(null);
  const offsetRef = useRef<number | null>(null); // serverUtcMs minus deviceUtcMs

  useEffect(() => {
    let cancelled = false;

    // Sync with the server: computes the offset from the device clock and stores it.
    async function sync() {
      try {
        const t = await api.getTimeNow();
        if (cancelled) return;
        offsetRef.current = new Date(t.utc).getTime() - Date.now();
        setMeta(t);
        setError(null);
      } catch (e) {
        if (cancelled) return;
        setError(e instanceof Error ? e.message : "מקור הזמן אינו זמין");
      }
    }

    sync();
    const resync = setInterval(sync, resyncMs);     // periodic re-sync
    const tick = setInterval(() => {                // local tick every second for smooth display
      if (offsetRef.current != null) {
        setNow(new Date(Date.now() + offsetRef.current));
      }
    }, 1000);

    return () => {
      cancelled = true;
      clearInterval(resync);
      clearInterval(tick);
    };
  }, [resyncMs]);

  return { now, meta, error };
}
