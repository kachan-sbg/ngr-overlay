# Phase 12 вЂ” OBS Mode & Enhanced UX

> **Goal:** Polish the OBS/streaming workflow and add UX features that bring NrgOverlay
> to competitive parity with established apps.
>
> **OBS Mode approach for Alpha:** The existing stream override system (single window, toggle
> between driver/OBS appearance) is the correct approach. This phase enhances the toggle UX,
> ensures all new overlays have full stream override support, and makes the workflow seamless.
> True simultaneous dual-view (different driver + OBS layouts at the same time) is a post-Alpha
> goal вЂ” see architecture notes below.

## Status: `[ ]` Not started

---

### Context: OBS Mode Design Decision

For Alpha, we keep the single-window toggle approach:
1. Driver configures two profiles per overlay: **Screen** (their view) and **OBS** (viewer view)
2. Before going live, driver toggles "OBS Mode" вЂ” all overlays switch to their OBS profile
3. OBS captures what's on screen вЂ” the OBS profile
4. After streaming, driver toggles back

This is how SimHub, iOverlay, and most competitors work. It covers the primary use case
(streamers who want a richer overlay for viewers) with zero architectural complexity.

**Post-Alpha options for true simultaneous dual-view:**
- **Web source approach:** Built-in HTTP server serves overlay data as JSON; bundled HTML/CSS/JS
  renders the OBS version as a Browser Source in OBS. Completely independent rendering.
- **Second window approach:** Two HWNDs per overlay; stream window on a secondary monitor or
  off-screen. OBS captures the stream window. Requires multi-monitor setup.
- Decision deferred to post-Alpha based on user demand.

---

### TASK-1201 В· OBS Mode UX refinement

**Status:** `[ ]`

Rename "Stream Mode" to "OBS Mode" throughout the UI and make the toggle more prominent.

**What to build:**
- Rename all UI references: "Stream Mode" в†’ "OBS Mode", "Stream Override" в†’ "OBS Profile"
- Tray icon: "OBS Mode" checkbox (already exists as "Stream mode")
- Add OBS Mode status indicator: when active, overlay title bars in edit mode show "(OBS)" suffix
- Hotkey for OBS Mode toggle (TASK-1203)
- Ensure every Alpha overlay (Input, Fuel, Standings, Pit, Weather, Flat Map) has full `StreamOverrideConfig` support for all its config fields
- Settings UI: "OBS Profile" tab per overlay with clear labeling
- First-time hint: when user enables OBS Mode for the first time, show a brief tooltip explaining the workflow

**Acceptance criteria:**
- [ ] All UI says "OBS Mode" / "OBS Profile" (not "Stream")
- [ ] OBS Mode toggle works from tray, settings, and hotkey
- [ ] Edit mode shows "(OBS)" in overlay title area when OBS Mode is active
- [ ] All 9 overlays (3 MVP + 6 Alpha) have full OBS Profile support
- [ ] Switching modes is instant (same as current stream mode toggle)
- [ ] Config fields: `streamModeActive` renamed to `obsModeActive` in config JSON (migration in TASK-701)

**Dependencies:** TASK-706 (all overlays registered), Phase 10/11 (all overlays implemented).

---

### TASK-1202 В· Session-type profiles

**Status:** `[ ]`

Auto-switch overlay configurations based on session type (Practice, Qualifying, Race).

**What to build:**
- Each overlay config gains an optional `sessionProfiles` map:
  ```json
  "sessionProfiles": {
    "Practice": { "showIRating": true, "showBestLap": true },
    "Race": { "showIRating": false, "maxDriversShown": 20 }
  }
  ```
- Session profile is a partial override (same null-coalescing pattern as stream override)
- When `SessionData.SessionType` changes, overlays resolve: `sessionProfile ?? base config`
- OBS profile stacks on top: `obsProfile ?? sessionProfile ?? base`
- Config resolution order: base в†’ session profile в†’ OBS profile
- Settings UI: tab or dropdown per session type to configure overrides

**Acceptance criteria:**
- [ ] Overlay appearance changes automatically on session type transition
- [ ] Practice в†’ Qualifying в†’ Race transitions handled
- [ ] Null fields inherit from base (same pattern as OBS profile)
- [ ] OBS profile stacks on top of session profile
- [ ] Settings UI allows configuring per-session overrides
- [ ] Config migration handles the new section
- [ ] Session profiles affect visual properties only (not position)

**Dependencies:** Phase 7 (config versioning), TASK-706 (overlay registration).

---

### TASK-1203 В· Global hotkey system

**Status:** `[ ]`

Configurable keyboard shortcuts for overlay control.

**What to build:**
- Replace hardcoded F9/F10 hotkeys with a configurable hotkey system
- Hotkey actions:
  - Toggle edit mode (default: Ctrl+Shift+E)
  - Toggle OBS mode (default: Ctrl+Shift+O)
  - Show/hide all overlays (default: Ctrl+Shift+H)
  - Open settings (default: F9)
  - Per-overlay toggle visibility (no defaults вЂ” user assigns)
  - Exit (no default вЂ” tray only, or user assigns)
- Config section in `GlobalSettings`:
  ```json
  "hotkeys": {
    "toggleEditMode": "Ctrl+Shift+E",
    "toggleObsMode": "Ctrl+Shift+O",
    "toggleAllOverlays": "Ctrl+Shift+H",
    "openSettings": "F9"
  }
  ```
- `MessagePump`: parse hotkey strings в†’ `RegisterHotKey` with correct modifier + virtual key
- Handle conflicts: if `RegisterHotKey` fails, log a warning and show in Settings

**Acceptance criteria:**
- [ ] All listed actions work via configured hotkeys
- [ ] Hotkeys configurable in Settings UI
- [ ] Modifier keys supported: Ctrl, Shift, Alt
- [ ] Conflicting hotkeys detected and reported in Settings
- [ ] F9/F10 defaults replaced with configurable alternatives
- [ ] Per-overlay toggle works for any registered overlay

**Dependencies:** TASK-706 (overlay registration for per-overlay toggles).

---

### TASK-1204 В· Buddy / friend list

**Status:** `[ ]`

Highlight specific drivers in Relative and Standings overlays.

**What to build:**
- New config section: `buddyList: string[]` (driver names or customer IDs)
- Matching: by driver name substring (case-insensitive) or exact customer ID
- Highlight: buddy rows get a distinct background color (configurable `buddyHighlightColor`)
- Applies to both Relative and Standings overlays
- Settings UI: text area for entering buddy names/IDs, one per line

**Acceptance criteria:**
- [ ] Buddy drivers highlighted in Relative overlay
- [ ] Buddy drivers highlighted in Standings overlay
- [ ] Matching works by name substring or customer ID
- [ ] Buddy highlight color configurable
- [ ] Buddy list editable in Settings
- [ ] Mock data includes one buddy entry for preview

**Dependencies:** TASK-1003 (Standings overlay).

---

### TASK-1205 В· Column customization for Relative and Standings

**Status:** `[ ]`

Allow users to show/hide and reorder columns in tabular overlays.

**What to build:**
- Per-overlay config: `columnOrder: string[]` listing column IDs in display order
  - Relative columns: `["pos", "car", "name", "irating", "license", "gap", "lap"]`
  - Standings columns: `["pos", "class", "car", "name", "irating", "gap", "best"]`
- Columns not in the list are hidden
- Overlays render columns in the order specified
- Settings UI: checklist of columns with up/down arrows for reorder
- Column widths auto-adjust based on which columns are visible

**Acceptance criteria:**
- [ ] Columns render in the order specified by config
- [ ] Hidden columns don't take up space
- [ ] Column order configurable in Settings UI
- [ ] Reorder persists to config
- [ ] Works for both Relative and Standings overlays
- [ ] OBS profile can specify a different column order

**Dependencies:** TASK-1003 (Standings), Phase 7 (config).

---

### TASK-1206 В· Positions gained/lost indicator

**Status:** `[ ]`

Show how many positions each driver has gained or lost since race start.

**What to build:**
- Track starting position per driver (captured from session data at race start)
- Compute delta: `startPosition - currentPosition` (positive = gained, negative = lost)
- Display as small `+N` (green) or `-N` (red) or `вЂ”` (unchanged)
- Only active during Race sessions; hidden in Practice/Qualifying

**Acceptance criteria:**
- [ ] Starting positions captured at race start
- [ ] Gains shown in green, losses in red
- [ ] Zero change shown as "вЂ”" or hidden
- [ ] Only visible during Race sessions
- [ ] Works in both Relative and Standings overlays
- [ ] Handles mid-race join (no starting position в†’ show "вЂ”")

**Dependencies:** TASK-1003 (Standings).

---

### TASK-1207 В· Multi-class color scheme configuration

**Status:** `[ ]`

Allow users to customize the colors assigned to each car class.

**What to build:**
- Config section: `classColors: { "GTP": "#FF6600", "LMP2": "#0066FF", ... }`
- Default palette: auto-generated from session data class colors
- Settings UI: class name + color picker for each class detected
- Colors applied in: Relative (class badge), Standings (class separator/badge), Flat Map (car markers)
- Persistent: colors saved per class name, survive across sessions

**Acceptance criteria:**
- [ ] Class colors configurable in Settings
- [ ] Custom colors applied across all multi-class overlays
- [ ] Default colors sourced from session data
- [ ] Custom colors persist and override session defaults
- [ ] Unknown classes get an auto-assigned color from a default palette

**Dependencies:** TASK-705 (multi-class data model), TASK-1003 (Standings), TASK-1103 (Flat Map).

---

