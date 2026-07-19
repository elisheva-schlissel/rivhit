// Types matching the API's JSON contracts (fields in English — as they arrive from the server).

export interface Employee {
  id: number;
  employeeNumber: string;
  fullName: string;
  email?: string | null;
}

// The authoritative Zurich time as returned by the server.
export interface TimeNow {
  utc: string;
  zurich: string;
  zurichDisplay: string;
  source: string;
  isFallback: boolean;
}

// An employee's punch status.
export interface Status {
  employeeId: number;
  fullName: string;
  isClockedIn: boolean;
  openRecordId: number | null;
  clockedInSince: string | null;
  openDurationMinutes: number | null;
}

// An attendance record in the history table.
export interface AttendanceRecord {
  id: number;
  employeeId: number;
  employeeName: string;
  clockInZurich: string;
  clockInSource: string;
  clockInIsFallback: boolean;
  clockOutZurich: string | null;
  clockOutSource: string | null;
  clockOutIsFallback: boolean;
  isOpen: boolean;
  durationMinutes: number | null;
}

// The result of a punch action.
export interface ClockActionResult {
  action: string;
  record: AttendanceRecord;
  time: TimeNow;
}
