# Survivor HUD calibration

Hook detection uses screenshots of the foreground Dead by Daylight client area.
It does not read game memory. The capture uses physical screen pixels, including
the window's position and Windows DPI scaling. Default horizontal coordinates
use a 16:9 reference width so ultrawide screens do not stretch the HUD coordinates.
Defaults are estimates; calibrate for your HUD scale and icon appearance.

## Setup

1. Use borderless or windowed mode. Start a custom match with all survivor slots visible.
2. Open **Killer overlay**, select the correct **2v8 mode**, then **Calibrate region**.
   The app minimizes briefly and captures the foreground game. If another app gets
   focus, return to the game and retry.
3. Drag a narrow column from the top of the first central survivor state icon to
   the bottom of the last. Include empty slots. Exclude names, hook tally marks
   and decorative portrait borders. Check every green box before **Save region**.
4. Use **Learn unhooked state** to save visible healthy and injured appearances
   for the survivors in this match. Select the corresponding slot. Do not learn
   a hooked, terminal, obscured or empty slot. Known hook/terminal templates and
   flat images are rejected, but you must verify the selected image visually.
5. Enable hooks and/or the timer. Use **Reset values** at the start of each match,
   including consecutive matches on the same map. Three consecutive observations
   (approximately 0.6 seconds) confirm each state transition.

An unrecognized image does **not** imply an unhook. A timer starts only after a
confirmed hooked state becomes a recognized unhooked appearance. Learned portraits
can change with characters, injuries, cosmetics and HUD replacements; learn the
new appearance if detection stays uncertain. This intentionally favors a missed
detection over an incorrect timer.

Four- and eight-survivor regions and learned images are separate. Regions persist
in application user settings. Learned crops stay under
`%LOCALAPPDATA%\DBDOverlay\Calibration\four` or `eight` (maximum 32 per mode).
Saving a new region or **Reset calibration** clears that mode's learned images.
**Reset values** only clears counts and cancels timers; it keeps calibration.

## Validation

On Windows, with the .NET 10 SDK and .NET Framework 4.8 runtime:

```powershell
dotnet build DBDOverlay/DBDOverlay.csproj -c Release
dotnet test DBDOverlay.Tests/DBDOverlay.Tests.csproj -c Release
```

Tests cover 1080p, 1440p, ultrawide, negative monitor offsets, physical-pixel
scaling, profile persistence, actual shipped icon fixtures, empty images,
confirmed transitions, terminal states, timer cancellation and worker restarts.
Windows CI runs the same build and tests.

Automated tests do not establish recognition accuracy on live gameplay. Before
considering the change ready, validate the calibration preview at the real HUD
scale, restart the app to check persistence, then hook and unhook a survivor in
a custom match. Check that one hook is counted, the timer starts once, and
**Reset values** stops it. Repeat with the alternate mode and DPI scaling where available.

STEAXS mappings, map recognition and ReShade F10 reload behavior are unchanged.
Do not commit personal presets, user.config files, learned crops or application logs.

## Updating an existing installation

Close DBD Overlay and back up its entire installation directory and its
`%LOCALAPPDATA%\DBDOverlay` user-settings directory. Copy Release executable and
dependencies while preserving presets and settings. If dependencies introduce
assembly redirects, merge the generated configuration's `runtime` element only;
do not replace existing settings or map-to-preset associations.

To restore, close the modified app and restore both saved directories. ReShade
does not need reinstalling; this change does not modify its files.
