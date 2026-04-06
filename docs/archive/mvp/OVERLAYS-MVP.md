# OVERLAYS.md

## Racing Simulator Overlay — Overlay Specifications

### Design Principles

All overlays follow the TinyPedal aesthetic:
- Dark semi-transparent background (default: ~75% opaque black).
- White or light-grey text.
- Monospaced font for tabular alignment (default: Consolas 13px).
- No window chrome, no title bar, no resize handles visible during locked mode.
- Padding: 8px internal padding on all sides.
- Row height: font size + 6px (e.g., 19px rows at 13px font).

### Overlay 1: Relative

**Purpose**: Show approximately 15 drivers positioned relative to the player on track (by track position percentage), with the player's row highlighted.

**Window defaults**: 500 × 380 px. Minimum 300 × 200 px. Maximum 1200 × 900 px.

**Update frequency**: 10 Hz (every 100 ms). Sufficient for relative gaps and position changes.

**Layout**:

```
┌─────────────────────────────────────────────────────────┐
│  POS  CAR   DRIVER NAME         iRTG  LIC    GAP    LAP │
│───────────────────────────────────────────────────────── │
│   1   #12   J. Villeneuve       4521  A 3.2  -4.23   0  │
│   2   #7    M. Schumacher       6103  A 4.8  -2.10   0  │
│   3   #44   L. Hamilton         7892  Pro    -0.88   0  │
│ ► 4   #33   M. Verstappen       5234  A 2.1   0.00   0  │  ← player row (highlighted)
│   5   #11   S. Perez            4891  B 4.1  +1.45   0  │
│   6   #55   C. Sainz            4102  B 2.7  +3.12   0  │
└─────────────────────────────────────────────────────────┘
```

**Column definitions**:

| Column | Field | Width | Alignment | Notes |
|--------|-------|-------|-----------|-------|
| POS | `RelativeEntry.Position` | 4 chars | Right | Overall position in session |
| CAR | `RelativeEntry.CarNumber` | 4 chars | Right | Car number with `#` prefix |
| DRIVER NAME | `RelativeEntry.DriverName` | 20 chars | Left | Truncated with ellipsis if longer |
| iRTG | `RelativeEntry.IRating` | 5 chars | Right | Show `----` if unavailable |
| LIC | `RelativeEntry.LicenseLevel` | 6 chars | Left | Colored background cell matching license class color |
| GAP | `RelativeEntry.GapToPlayerSeconds` | 7 chars | Right | `±X.XX` format. "LEADER" for P1 player row in race |
| LAP | `RelativeEntry.LapDifference` | 4 chars | Right | `0`, `+1`, `-1`, etc. |

**License class colors** (background of the LIC cell):
- R (Rookie): `#FF4444`
- D: `#FF8800`
- C: `#FFFF00` (text black)
- B: `#00BB00`
- A: `#0088FF`
- Pro: `#9944FF`
- WC: `#FF44FF`

**Player row**: highlighted with a subtly brighter background fill, e.g., the overlay background color lightened by 30%, or a distinct accent color configurable by the user. A `►` marker in a leading column.

**Driver selection**: The list is centered on the player. If there are N slots available:
- Show `floor(N/2)` drivers ahead of the player (by track position).
- Show `ceil(N/2) - 1` drivers behind.
- If fewer drivers exist ahead/behind, fill from the other direction.
- In race mode, "ahead/behind" is by track position percentage gap, not overall position.

**Configurable per overlay**:
- `enabled` (bool)
- `x`, `y` (position)
- `width`, `height`
- `opacity` (0.0–1.0)
- `backgroundColor` (RGBA)
- `textColor` (RGBA)
- `fontSize` (10–48 px)
- `showIRating` (bool) — hide iRTG column to save space
- `showLicense` (bool) — hide LIC column
- `maxDriversShown` (int, 5–21, default 15)
- `playerHighlightColor` (RGBA)

---

### Overlay 2: Session Info

**Purpose**: Display static-ish session metadata and driver summary statistics in a compact panel.

**Window defaults**: 260 × 280 px. Minimum 180 × 150 px. Maximum 800 × 600 px.

**Update frequency**: 1 Hz for session/weather data; 60 Hz for lap times and delta (lap times are read from `DriverData`).

**Layout**:

```
┌────────────────────────────────┐
│ Silverstone GP                 │
│ Race · 25 laps remaining       │
│ Session  01:23:45              │
│ Clock    14:32:07              │
│ Game     14:45 (afternoon)     │
│ ────────────────────────────── │
│ Air      22.1°C                │
│ Track    38.7°C                │
│ ────────────────────────────── │
│ Lap      12 / 50               │
│ Last     1:34.521              │
│ Best     1:33.887              │
│ Delta   -0.034                 │
└────────────────────────────────┘
```

**Field definitions**:

| Label | Source | Update Rate | Notes |
|-------|--------|-------------|-------|
| Track name | `SessionData.TrackName` | On change | e.g., "Silverstone GP" |
| Session type + remaining | `SessionData.SessionType`, `SessionData.SessionTimeRemaining` | 1 Hz | Time-based: "MM:SS remaining". Lap-based: "N laps remaining" |
| Session elapsed | `SessionData.SessionTimeElapsed` | 1 Hz | "HH:MM:SS" |
| Clock | `DateTime.Now` | 1 Hz | Local wall clock, "HH:mm:ss" |
| Game time of day | `SessionData.GameTimeOfDay` | 1 Hz | "HH:mm" + descriptor ("morning", "afternoon", etc.) |
| Air temp | `SessionData.AirTempC` | On change | "XX.X°C" |
| Track temp | `SessionData.TrackTempC` | On change | "XX.X°C" |
| Lap number | `DriverData.Lap` | 60 Hz | "current / total" if total known |
| Last lap | `DriverData.LastLapTime` | On new lap | "M:SS.mmm" or "--:--.---" if no lap completed |
| Best lap | `DriverData.BestLapTime` | On improvement | "M:SS.mmm" |
| Delta | `DriverData.LapDeltaVsBestLap` | 60 Hz | "±X.XXX" seconds. Colored: green if negative (faster), red if positive (slower) |

**Configurable per overlay**: same base set. Additionally:
- `showWeather` (bool)
- `showDelta` (bool, default true — delta row)
- `showGameTime` (bool)
- `use12HourClock` (bool)
- `temperatureUnit` (enum: Celsius, Fahrenheit)

---

### Overlay 3: Delta Bar

**Purpose**: A focused, large-format real-time delta display showing how the current lap compares to the player's best lap. Intended to be placed near the center of the screen for quick glances.

**Window defaults**: 300 × 80 px. Minimum 150 × 50 px. Maximum 800 × 200 px.

**Update frequency**: 60 Hz.

**Layout**:

```
┌──────────────────────────────────────┐
│              -0.234                  │
│   ◄ ■■■■■■▓▓▓▓|                     │  ← bar centered on midpoint; fill direction indicates delta sign
└──────────────────────────────────────┘
```

Detailed layout description:

1. **Delta value text**: Centered horizontally above (or overlaid on) the bar. Format: `+X.XXX` or `-X.XXX` seconds. Font: larger than base (default 20px). Color: green (`#00DD00`) if delta is negative (faster than best), red (`#DD2222`) if positive (slower).

2. **Delta bar**:
   - A horizontal bar spanning the full width of the overlay's inner content area.
   - A vertical center line marks zero delta.
   - When delta is negative (faster): bar fills to the LEFT of center, colored green. Fill width is proportional to the delta magnitude.
   - When delta is positive (slower): bar fills to the RIGHT of center, colored red.
   - Maximum visible bar fill corresponds to a configurable `deltaBarMaxSeconds` (default: ±2.0 seconds). Beyond this, the bar is clamped at full width.
   - The fill is drawn with a slight gradient: brighter at the zero-crossing edge, slightly dimmer at the outer edge.
   - The bar itself has a dark background (slightly lighter than the overlay background) to show the empty portion.

3. **Delta trend indicator** (optional, configurable):
   - A small triangle or arrow beside the delta value indicating whether the delta is increasing or decreasing in magnitude over the last 500 ms.
   - ▲ = gap is increasing (getting slower relative to best); ▼ = gap is decreasing (getting faster).

**Delta calculation note**: `LapDeltaVsBestLap` from iRacing SDK is a direct value from the sim. It represents the real-time delta between the player's current position on-track (by distance) and the time they crossed that same point on their best lap. A negative value means they are ahead of their best lap pace at this point.

**Bar animation**: because `LapDeltaVsBestLap` updates at 60 Hz from the sim, no additional interpolation is needed. The bar position directly maps from the current value. The color transition (green/red) updates instantly when the sign changes.

**Configurable per overlay**:
- Standard base set.
- `deltaBarMaxSeconds` (float, 0.5–5.0, default 2.0) — value at which bar is fully filled.
- `fasterColor` (RGBA, default green) — bar and text color when faster than best.
- `slowerColor` (RGBA, default red) — bar and text color when slower.
- `showTrendArrow` (bool, default true).
- `showDeltaText` (bool, default true).

---