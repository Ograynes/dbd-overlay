# Local edition

This branch is a local customization, not an upstream release. Automatic upstream updates are disabled to keep this build installed.

## ReShade integration

The redesigned page keeps the existing dark/red theme and adds a scrollable layout, status messages and named map associations. Legacy associations migrate on first launch; refreshing the preset list no longer changes associations when file order changes. Disconnecting a folder does not delete its files.

Enable **Automatically apply prepared filters** in ReShade integration to queue a reload after map detection prepares the overlay preset. Select the game's `ReShade.ini` and keep the overlay preset selected in ReShade. The option is off by default and persists across restarts.

The reload uses the configured unmodified F1–F24 key, usually F10. It waits up to 30 seconds for Dead by Daylight in the foreground and for held modifiers to be released. A key press is sent once; no game memory is accessed. Manual reload remains available. A status saying the key was sent does not prove that ReShade rendered the filter: verify this in a custom match.

ReShade polls keyboard state on rendered frames, so the key pulse is held briefly before release. See upstream [input handling](https://github.com/crosire/reshade/blob/main/source/input.cpp) and [runtime reload handling](https://github.com/crosire/reshade/blob/main/source/runtime.cpp).

## Other changes

- Resizable main window, title-only dragging, scrollable settings and themed calibration controls.
- Consistent close/Alt+F4 cleanup and a single application instance.
- Startup failures show the underlying error instead of opening partially initialized UI.
- OCR captures relative to the game window, serializes engine access and disposes temporary images; manual recognition runs off the UI thread.
- Safe empty map navigation and foreground monitoring on the UI dispatcher.
- Atomic preset replacement and explicit error messages; personal presets are retained.

## Validation

Run `dotnet test DBDOverlay.Tests/DBDOverlay.Tests.csproj -c Release` on Windows. The 78-test suite covers HUD geometry, matching and tracking, startup resources, preset migration/copying, reload configuration and queue behavior, key release, OCR image isolation, and WPF page layout at two widths. Main window construction is also exercised.

Manual checks still required: enable automatic map recognition, enter a custom match, confirm that the correct filter appears without pressing F10, then verify a hook/unhook with the calibrated HUD. Automated tests do not replace these game checks.
