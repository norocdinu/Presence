# Presence

A low-profile keep-awake / keep-active tray utility for Windows. It stops the PC
from sleeping and (optionally) keeps your session showing as "active," without
the tell-tale fixed-interval fake keypresses that a basic jiggler produces.

## How it works

| Goal | Mechanism | Detectable? |
|------|-----------|-------------|
| Keep the **machine awake** | `SetThreadExecutionState` (the OS power API — same call media players use) | No synthetic input at all; nothing for an input hook to see. |
| Keep the **display on** | `ES_DISPLAY_REQUIRED` flag on the same call | Same as above. |
| Keep you **"active"** (Teams/Slack green) | Invisible **zero-pixel mouse move**, fired **only while you're genuinely idle**, at **randomized intervals** | Minimal: no cursor movement, no fixed period, never fights real input. |

Why the split: `SetThreadExecutionState` keeps the computer awake but does **not**
reset the OS idle timer (`GetLastInputInfo`), which is what Teams/Slack use to
flip you to "Away." Holding presence green needs a tiny input nudge — so that is
a separate, optional toggle.

No admin rights, no driver, no network, no installer. One user-mode process.
The exe/process name has no effect on behaviour — rename it to anything.

## Usage

1. Run `Presence.exe` (or the desktop shortcut). A tray icon appears
   (green = holding, gray = paused/stopped).
2. **Right-click** the tray icon for all options. **Double-click** = pause/resume.

### Tray menu

- **Active / Paused** — master pause toggle.
- **Keep system awake** — hold the machine awake (power API). *Default on.*
- **Keep display on** — also keep the monitor from turning off. *Default on.*
- **Keep me active (presence)** — nudge the idle timer so you stay "active."
  *Default on.*
- **Auto-stop** — off, a preset (5:00 PM … 11:00 PM), or **Custom…** (enter
  `HH:mm`, 24-hour). After this time each day the tool releases everything and
  goes dormant.
- **Launch at startup** — adds/removes an `HKCU\...\Run` entry so it starts at
  login.
- **Open config folder** — opens `%APPDATA%\Presence`.
- **Exit** — releases the power hold and quits.

## Configuration file

Stored at `%APPDATA%\Presence\config.ini`, written when you change a setting.
You can also edit it by hand (restart Presence to apply):

```ini
KeepAwake=True          # hold the machine awake via the power API
KeepDisplayOn=True      # also keep the monitor on
KeepActive=True         # nudge the idle timer so presence stays "active"
IdleThreshold=50        # only nudge after this many seconds of REAL idle
MinInterval=40          # nudge no sooner than this many seconds apart
MaxInterval=110         # ...and no later than this (randomized in between)
StopEnabled=False       # auto-stop at a time of day
StopTime=18:00          # HH:mm, 24-hour
StartAtLogin=False      # run at login (kept in sync with the tray toggle)
```

Tune `IdleThreshold`/`Min`/`Max` to taste. Larger, more varied intervals = a
lighter, less patterned footprint; smaller = more aggressively keeps you green.

## Verifying it's holding the PC awake

With Presence running, open an **elevated** terminal and run:

```
powercfg /requests
```

You'll see `Presence.exe` under **SYSTEM** (and **DISPLAY** if "Keep display on"
is enabled). `powercfg /requests` requires admin — that's a Windows restriction,
not the tool.

## Building from source

Needs only the .NET Framework that ships with Windows (no SDK/installer):

```
build.cmd
```

This invokes the in-box `csc.exe` and produces `Presence.exe`.

## Files

- `Presence.exe` — the compiled tray app
- `Presence.cs` — source
- `build.cmd` — rebuild script
- `README.md` — this file
