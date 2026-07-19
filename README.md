# שעון נוכחות — Europe/Zurich

מערכת Full-Stack להחתמת נוכחות של עובדים (כניסה / יציאה — Clock In / Clock Out), שנבנתה
מקצה לקצה באמצעות Claude Code.

כל החתמה מתועדת בזמן המתקבל מ**שירות זמן חיצוני** עבור אזור הזמן **`Europe/Zurich`** —
לעולם לא משעון הדפדפן ולא משעון השרת המקומי, כנדרש.

| שכבה | טכנולוגיה |
|------|-----------|
| Frontend | React 18 + TypeScript + Vite |
| Backend | ASP.NET Core 8 Web API (controllers) + EF Core 8 |
| מסד נתונים | Microsoft SQL Server (LocalDB מוכן לשימוש מיידי) |
| מקור זמן | שירותי זמן חיצוניים ב‑HTTP (Akamai ← Cloudflare ← worldtimeapi.org ← timeapi.io) |

> 📄 למסמך ארכיטקטורה מפורט ראו [ARCHITECTURE.md](ARCHITECTURE.md).

---

## למה ארכיטקטורת הזמן בנויה כך

הדרישה הקשיחה היחידה: **הזמן הקובע לכל Clock In / Clock Out חייב להגיע מ‑API חיצוני עבור
`Europe/Zurich`; אין להשתמש בשעון הדפדפן או בשעון השרת המקומי.**

החלטות התכן הנגזרות מכך:

1. **ה‑Backend הוא הבעלים של הזמן, לא הדפדפן.** אפליקציית ה‑React לעולם אינה קוראת `Date.now()`
   כדי לרשום החתמה. בלחיצה על כניסה/יציאה, ה‑Backend פונה לשירות הזמן החיצוני *באותו רגע* ושומר
   את מה שחזר. המחוון בכותרת "מתקתק" בדפדפן לתצוגה בלבד, והוא *מעוגן לקריאת שרת* (`GET /api/time/now`)
   ומסונכרן מחדש כל 60 שניות — הוא לעולם לא מקור לחותמת זמן נשמרת.

2. **שרשרת ספקים עם fallback מדורג** (`ZurichTimeProvider`). שירותי זמן ציבוריים חסומים לעיתים
   קרובות ברשתות ארגוניות, לכן הספק מנסה מספר מקורות לפי סדר:
   `time.akamai.com` (‏ISO-8601 UTC) ← `cloudflare.com/cdn-cgi/trace` (‏epoch UTC) ←
   `worldtimeapi.org` ← `timeapi.io`. שני הראשונים מספקים את **הרגע** הקובע ב‑UTC; שעון הקיר של
   ציריך נגזר לאחר מכן דרך **מסד אזורי הזמן של IANA** (`Europe/Zurich`), הנושא את כלל **שעון הקיץ
   (DST)** הנכון (למשל `+02:00` בקיץ, `+01:00` בחורף). שני האחרונים "מדברים" ישירות בזמן ציריך.
   בכל מקרה — כל המקורות חיצוניים, אף אחד אינו שעון השרת הגולמי.

3. **מודל האחסון** (`AttendanceRecord`). הרגע הקובע נשמר ב‑**UTC**, ולצידו שעון הקיר של ציריך, שם
   ה**מקור**, ודגל **is-fallback** לצורכי תיעוד. שמירת UTC + מקור שומרת על נכונות הנתונים ועל
   יכולת מעקב גם מעבר לגבולות שעון הקיץ.

4. **חוסן ללא רמאות** (`AllowCachedOffsetFallback`). אם *כל* הספקים החיים נופלים לרגע, השירות משחזר
   את הזמן מה‑**offset של הקריאה החיצונית המוצלחת האחרונה** (חלון טריות מוגדר, ברירת מחדל 60 דקות)
   ומסמן את ההחתמה כ‑`isFallback = true`, כך שהיא גלויה בממשק וברשומה. אם אין עוגן חיצוני טרי כלל,
   ההחתמה **נדחית עם HTTP 503** במקום לסמוך בשקט על שעון השרת.

### מקרי קצה שטופלו

- **Clock In כפול** → `409 Conflict` (כבר מוחתם).
- **Clock Out ללא משמרת פתוחה** → `409 Conflict`.
- **מירוץ Clock In מקבילי** → **אינדקס ייחודי מסונן** (`WHERE ClockOutUtc IS NULL`) מבטיח לכל היותר
  משמרת פתוחה אחת לעובד ברמת מסד הנתונים, מאחורי הבדיקה ברמת השירות.
- **משמרות חוצות חצות / לילה** → המשך מחושב מרגעי UTC, כך שהוא חוצה חצות נכון.
- **מעברי שעון קיץ (DST)** → ה‑offset מגיע ממסד אזורי הזמן של IANA ברגע ההחתמה.
- **סחף שעון (יציאה מוקדמת מכניסה)** → המשך מקובע לערך אי‑שלילי.
- **מקור הזמן אינו זמין** → `503`, ולא נוצרת רשומה.
- **עובד לא פעיל / לא קיים** → `404`.

---

## דרישות מקדימות

- **‏.NET SDK 8** (‏`dotnet --version`)
- **‏Node.js 18+** ו‑npm (עבור ה‑Frontend)
- **‏SQL Server LocalDB** (מגיע עם Visual Studio / SQL Server Express) — או ערכו את מחרוזת החיבור
  בקובץ [`appsettings.json`](src/AttendanceSystem.Api/appsettings.json) כך שתפנה לכל SQL Server אחר.
- גישת HTTPS יוצאת לפחות לאחד משירותי הזמן שלעיל.

---

## הרצת הפרויקט

### 1. Backend (‏API + מסד נתונים)

```bash
cd src/AttendanceSystem.Api
dotnet run
```

- בעליית השרת המערכת **מריצה מיגרציות של EF Core** ו**מזריעה חמישה עובדים לדוגמה**.
- ‏API: ‏`http://localhost:5138` — ממשק Swagger בכתובת ‏`http://localhost:5138/swagger`.

> משתמשים ב‑SQL Server אחר? עדכנו את `ConnectionStrings:Default` בקובץ `appsettings.json`.

### 2. Frontend

```bash
cd frontend
npm install
npm run dev
```

פתחו את ‏`http://localhost:5173`. שרת הפיתוח של Vite מנתב `/api` אל ה‑Backend, כך שהדפדפן פונה
לאותו origin.

### 3. בדיקות

```bash
dotnet test
```

מכסות את כללי הנוכחות באמצעות מקור זמן מזויף (ללא רשת): כניסה/יציאה, חישוב משך, החתמה כפולה,
משמרת חסרה, עובד לא פעיל, מקור זמן שאינו זמין, וקיבוע סחף שעון.

---

## ‏API

| Method | נתיב | תכלית |
|--------|------|-------|
| GET    | `/api/health`                         | בדיקת חיות |
| GET    | `/api/time/now`                       | השעה הקובעת הנוכחית של Europe/Zurich |
| GET    | `/api/employees`                      | רשימת עובדים פעילים |
| GET    | `/api/employees/{id}/status`          | מצב ההחתמה הנוכחי של עובד |
| POST   | `/api/attendance/clock-in`            | `{ "employeeId": 1 }` → פתיחת משמרת |
| POST   | `/api/attendance/clock-out`           | `{ "employeeId": 1 }` → סגירת המשמרת הפתוחה |
| GET    | `/api/attendance/records?employeeId=` | היסטוריית נוכחות (דוח שעות) |

שגיאות מוחזרות כ‑`{ "error": "<code>", "detail": "<message>" }` עם קוד סטטוס מתאים.

---

## מבנה הפרויקט

```
AttendanceSystem.sln
├─ src/AttendanceSystem.Api/         שרת ה‑ASP.NET Core Web API
│  ├─ Controllers/                   נקודות קצה: Time, Employees, Attendance
│  ├─ Services/                      ZurichTimeProvider (זמן חיצוני) + AttendanceService (כללים)
│  ├─ Data/                          DbContext של EF Core, מיגרציות, מזריע
│  ├─ Models/                        Employee, AttendanceRecord
│  ├─ Middleware/                    מיפוי חריגות דומיין → תגובות HTTP
│  └─ Dtos/                          חוזי ה‑API
├─ tests/AttendanceSystem.Tests/     בדיקות xUnit לכללי הנוכחות
└─ frontend/                         ממשק React + TypeScript (Vite)
```

---

## צעדים אפשריים להמשך

אימות משתמש לכל עובד (כל משתמש רואה רק את השעון שלו), דשבורד ניהול ודיווח עם סיכומים שבועיים
וייצוא CSV, אישור מנהל לתיקונים ידניים, מעקב הפסקות, ו‑geofencing.
