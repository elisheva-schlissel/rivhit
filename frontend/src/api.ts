import type {
  Employee,
  TimeNow,
  Status,
  AttendanceRecord,
  ClockActionResult,
} from "./types";

// Shape of the server's error responses (see ExceptionHandlingMiddleware).
interface ApiError {
  error: string;
  detail: string;
}

// An API call error with a status code and a logical error code.
export class ApiCallError extends Error {
  constructor(public status: number, message: string, public code?: string) {
    super(message);
  }
}

// Wraps fetch: adds headers, parses errors into a readable message, and returns typed JSON.
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api${path}`, {
    headers: { "Content-Type": "application/json" },
    ...init,
  });

  if (!res.ok) {
    let message = `הבקשה נכשלה (${res.status})`;
    let code: string | undefined;
    try {
      const body = (await res.json()) as ApiError;
      if (body?.detail) message = body.detail;
      code = body?.error;
    } catch {
      /* non-JSON error body */
    }
    throw new ApiCallError(res.status, message, code);
  }

  // 204 or empty body
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

// The collection of the system's API operations.
export const api = {
  getEmployees: () => request<Employee[]>("/employees"),
  getTimeNow: () => request<TimeNow>("/time/now"),
  getStatus: (employeeId: number) => request<Status>(`/employees/${employeeId}/status`),
  clockIn: (employeeId: number) =>
    request<ClockActionResult>("/attendance/clock-in", {
      method: "POST",
      body: JSON.stringify({ employeeId }),
    }),
  clockOut: (employeeId: number) =>
    request<ClockActionResult>("/attendance/clock-out", {
      method: "POST",
      body: JSON.stringify({ employeeId }),
    }),
  getRecords: (employeeId: number) =>
    request<AttendanceRecord[]>(`/attendance/records?employeeId=${employeeId}`),
};
