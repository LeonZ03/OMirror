# AGENTS.md

This file is the compact source of truth for AI agents working in this repository. Read it before traversing code or binaries.

## Project identity

- Name: `OMirror`.
- Platform: Windows desktop, WinForms on .NET Framework.
- Current UI version: `1.16.0`; bundled runtime expected: scrcpy `4.1` with ADB `37.0.1`.
- There is intentionally no `.csproj`: `build.ps1` invokes the .NET Framework `csc.exe` directly.
- Repository source is self-contained. `dist/` and the legacy `devices.local.txt` are local-only and ignored.
- Release tags use `vMAJOR.MINOR.PATCH`; the Windows asset is named `OMirror-MAJOR.MINOR.PATCH-windows-x64.zip` and contains the complete `OMirror` runtime folder, not only the executable.
- Starting with the next release after `1.16.0`, the packaged runtime folder must contain `Uninstall.exe` next to `OMirror.exe`. Version `1.16.0` intentionally remains unchanged and does not contain the uninstaller.

## Source map

- `OMirror.cs`: shared Apple-inspired light/dark UI tokens and controls, dynamic device-picker popover, current-device state, scrcpy launch/stop/restart, keyboard mode persistence, and mode normalization.
- `DeviceRegistry.cs`: USB-only ADB discovery/parser, read-only Android metadata lookup, atomic JSON device-store persistence, and device-registry self-tests.
- `TransferForm.cs`: themed two-pane PC/Android file browser, collapsible activity drawer, selection, transfer queue, collision confirmation, and large-directory batching.
- `AdbClient.cs`: quoted ADB execution, UTF-8 shell input, Unicode-safe file transfer, and tar-stream directory receive.
- `KeyboardCapture.cs`: low-level Windows keyboard hook active only when a tracked scrcpy window owns foreground focus.
- `ScreenPowerPolicy.cs`: conservative keyguard parsing and the rule that physical screen-off is allowed only after a confirmed unlock.
- `app.manifest`: `asInvoker`; do not elevate, because Windows blocks Explorer drag/drop into elevated windows.
- `build.ps1`: canonical build/package entry point.
- `Diagnostics.cs`: bounded in-memory diagnostics and redacted abnormal-exit bundles under `%LOCALAPPDATA%`.
- `StressModel.cs`, `StressRunner.cs`, and `tests/Run-StabilityStress.ps1`: deterministic lifecycle model test plus safe Reno6 live stress runner/replay entrypoint.
- `tests/Test-ScreenShortcut.ps1` and `tests/ScreenShortcutProbe.cs`: exercise the production shortcut sender against bundled SDL3 with 40 alternating OFF/ON chords, including a hidden window. This does not replace physical-phone testing.
- `tests/Test-ScreenPowerPolicy.ps1`: regression checks for locked, unlocked, transitioning, input-restricted, missing, and unknown OEM keyguard reports.
- `OMirror.exe --self-test`: non-interactive package check; exit `0` means ADB, scrcpy, USB parsing, and temporary device-store read/write passed; exit `2` means one of those checks failed.

## Configuration contract

Saved devices live only in `%LOCALAPPDATA%\OMirror\devices.json`. The file is written through a same-directory temporary file replacement, and the selected device is persisted by ADB serial rather than list index. Version 1.15.0 intentionally does not read or import `devices.local.txt`; never restore hard-coded serials, IP addresses, usernames, or absolute user paths in tracked source.

Keyboard, screen-off, always-on-top, theme, selected-device, per-device keyboard mode, and window-position settings also live under `%LOCALAPPDATA%\OMirror`; they are runtime state, not repository content.

## Verified implementation facts

- Mirror launch preset: USB serial, H.264, 60 fps, 16 Mbps, zero video buffer, no audio, `--keep-active`, 450×900 window. `--keep-active` uses scrcpy's session-scoped user-activity signalling instead of changing persistent Android timeout settings. Do not pass scrcpy's `--always-on-top`; the persisted control-panel checkbox applies `HWND_TOPMOST` or `HWND_NOTOPMOST` after the window is created and hot-applies the same state without restarting scrcpy.
- The persisted “仅熄手机屏幕” setting sends `Left Alt+O` / `Left Alt+Shift+O` to the existing scrcpy window via queued native key messages. Do not restart the mirror to restore screen power. Global injected input is susceptible to IME hooks; synchronous window key messages also lost Shift in a local SDL3 regression probe, whereas queued chords preserved it. Track the current scrcpy process's `Device display turned on/off` logs, including replies to superseded requests, so a late OFF reply still triggers correction. Request and session guards discard stale UI work. Do not treat ADB command success, Android `mScreenState=ON`, or a display-power acknowledgement alone as proof the physical panel stays lit. Never use right-click, toggling power key (`26`), a sleep/wake cycle, `--turn-screen-off`, or a second control-only scrcpy instance for this setting.
- While a mirror is running, a one-second timer schedules single-flight background keyguard checks. Locked, input-restricted, and unknown states force the physical panel ON in either keyboard mode; only a confirmed unlock permits the saved screen-off preference. `KEYCODE_WAKEUP` (`224`) restores logical wakefulness when needed. OPPO Android 15 testing showed that a one-shot wake, or repeated wake keys alone, does not prevent the roughly ten-second lock-screen timeout; `--keep-active` is required as well. The user confirmed continuous physical lock-screen illumination and automatic physical screen-off after unlocking with the preference enabled. Do not dismiss keyguard or alter credentials.
- An adopted scrcpy process has no readable stdout stream in the new control panel. For that path, confirm requested power against the `connectionType=Internal` block in `dumpsys SurfaceFlinger`; never accept the virtual scrcpy display's ON state as physical-panel evidence. Unknown display formats must not be treated as a successful switch.
- The Win32 `INPUT` interop union includes `MOUSEINPUT`, `KEYBDINPUT`, and `HARDWAREINPUT`, so `Marshal.SizeOf(INPUT)` is 40 bytes on x64 and 28 bytes on x86. Do not reduce it to the keyboard member: Windows rejects the undersized `SendInput` call with `ERROR_INVALID_PARAMETER` and screen-power shortcuts silently stop working.
- Screen-power shortcuts are posted directly to the existing SDL window; they do not change Windows foreground focus or inject global modifier keys.
- Reno6 repeatedly entered ADB `offline` with the ADB `37.0.0` bundled by scrcpy 4.1, including while no OMirror/scrcpy process was running. Starting the separately installed Platform Tools ADB `37.0.1-15733141` immediately restored `device`. Release builds must therefore pass `-AdbDir` and bundle `adb.exe`, `AdbWinApi.dll`, and `AdbWinUsbApi.dll` from that tested runtime.
- Periodic ADB probes run on the thread pool behind an interlocked single-flight guard so a slow/offline USB transport cannot freeze the WinForms UI.
- The main window presents one active device. Refresh asynchronously parses `adb devices -l` and performs read-only property queries for online phones. Windows may omit the `usb:` detail for wired phones, so discovery accepts ordinary hardware serials while excluding host/port serials, modern `_adb-tls` names, and emulators; wireless transports and emulators are never auto-added.
- The picker is a dynamic collection ordered as current saved device, other saved online devices, saved offline devices, new online devices, then new unauthorized/offline devices. It shows at most four rows before scrolling.
- A new online device is saved and selected only after the user clicks `连接`; this also starts its first mirror. Saved rows always expose a `···` menu. Deleting a record stops that device's mirror, invalidates delayed callbacks, removes its per-device mode/position, and never writes to the phone. A still-connected deleted phone immediately becomes an unsaved new device.
- Double-clicking the `OMirror` wordmark centers the active device's existing mirror window in the working area of the monitor containing the control panel. It preserves the mirror size and configured topmost state, updates the remembered coordinates, and never restarts scrcpy.
- The selector list is an owned non-activating tool window, anchored below the active-device row. A main-window click closes it without deactivating the control panel; Escape and external app activation also close it.
- Mirror lifecycle is `Stopped / Starting / Running / Stopping / Recovering`. Each process has a generation id, so stale exit callbacks cannot alter a new session. Exit code `2` (disconnect) and unexpected nonzero exits may retry twice after 750ms and 2s; manual stop and keyboard hot-restart never consume the retry budget.
- scrcpy stdout/stderr are read asynchronously. Normal runs retain a bounded memory tail; unexpected exit writes a redacted log. Keep only five diagnostics bundles, and never add diagnostics/artifacts to Git.
- Theme mode persists as `0=auto`, `1=light`, or `2=dark`. Auto reads Windows `AppsUseLightTheme`; `SystemEvents.UserPreferenceChanged` hot-applies semantic colors to the main form, picker, open transfer forms, grids, and DWM title bars.
- The main action is stateful: idle `投屏`, starting `连接中`, running `停止投屏`, and stopping `停止中`. Existing matching scrcpy processes are adopted instead of duplicated, and process exit updates the UI asynchronously.
- UI text uses `Microsoft YaHei UI` for crisp Chinese GDI rendering and the installed `Segoe UI Variable Display Semibold` face only for the Latin product wordmark. Standard actions are vector-drawn icons with tooltips and accessible names; no emoji or bitmap icon font is required.
- The application icon is the transparent pixel portrait in `assets/OMirror-icon-source.png`; `assets/OMirror.ico` contains native 16, 20, 24, 32, 40, 48, 64, 128, and 256 px frames. `build.ps1` embeds it as the executable icon; both `MainForm` and `TransferForm` explicitly load the associated executable icon for consistent title-bar and taskbar rendering.
- `ModernButton` is a fully owner-drawn `Control`, not a native `Button`, and intentionally has no rounded Win32 `Region`. The single anti-aliased paint boundary avoids DPI-scaled corner fringes while the custom accessibility object preserves push-button semantics and keyboard activation.
- `app.manifest` declares `PerMonitorV2,PerMonitor` plus the legacy `true/pm` fallback. Keep the existing `AutoScaleMode.Dpi` forms so Windows renders text and vectors at the monitor's native scale instead of bitmap-stretching the whole window.
- UI layout is designed at 96 DPI. Both forms suspend layout while constructing controls and declare `AutoScaleDimensions = 96×96` before resuming; adding controls after the initial form scaling produces undersized bounds at 150%. Owner-drawn main controls scale vector geometry at paint time. The file window uses DPI-scaled runtime layout, with file-row/column dimensions explicitly scaled and a bounded activity drawer that leaves visible file rows at minimum size.
- Input modes are deliberately different:
  - Short-phrase mode uses scrcpy default SDK keyboard and adds no `--keyboard`, `--raw-key-events`, or `--prefer-text` flag.
  - Numeric-candidate mode adds only `--keyboard=uhid`.
- Mode changes find matching `scrcpy.exe` processes through WMI, close them, wait about 700 ms, and restart them with the selected backend.
- The two keyboard backends are presented as one keyboard-accessible segmented selector; changing it still uses the existing hot-restart path.
- Per-device last backend is persisted. A delayed `KEYCODE_SHIFT_LEFT` is sent only for a confirmed UHID→SDK transition; never send it on unknown first-run state.
- Clipboard synchronization is scrcpy's default. Do not add `--no-clipboard-autosync`.
- Foreground-only keyboard mapping:
  - `Alt+Tab` → Android `APP_SWITCH` (`187`).
  - `Win` and `Ctrl+Esc` → Android `HOME` (`3`).
  - All hooks are released when the main form closes; no keys are logged.
- Android shared storage defaults to `/sdcard/Download`; quick links include `/sdcard/Download` and `/sdcard/DCIM`.
- The transfer activity panel defaults collapsed, expands automatically when a transfer starts, and remains manually collapsible while retaining task history.
- Remote listing uses one Toybox command: `find ... -printf '%M|%s|%T@|%p\n'`. Do not regress to per-entry `-exec stat`.
- A real Android 15 device with 12,122 Camera entries returned the batch metadata in about 2.7 seconds. UI renders 1,000 rows per batch and enables “更多” for the rest.
- Windows ADB may corrupt a Unicode basename when it synthesizes a remote/local destination name. Uploads therefore use an ASCII temporary remote name followed by a UTF-8 shell rename.
- Single-file downloads use an explicit ASCII local temporary path and then `File.Move` to the Unicode final name.
- Directory downloads stream `adb exec-out tar` into Windows `tar.exe`; extraction requires `--options hdrcharset=UTF-8` plus UTF-8 locale variables. This was verified with nested Chinese names and hash equality.
- Existing destination files require explicit UI confirmation. Files overwrite; directories merge on upload. Local recursive deletion is allowed only after verifying the resolved target remains inside the selected destination directory.

## Build and validation

Canonical build:

```powershell
.\build.ps1 -ScrcpyDir "C:\path\to\scrcpy-win64-v4.1" -AdbDir "C:\path\to\platform-tools"
```

Expected output:

```text
dist\OMirror\OMirror.exe
dist\OMirror\Uninstall.exe
dist\OMirror\scrcpy\...
```

`Uninstall.exe` is a required deliverable for releases after `1.16.0`. It must remove the installed OMirror program files and OMirror shortcuts, then offer a clear choice to retain or delete `%LOCALAPPDATA%\OMirror` device records, preferences, and diagnostics. It should also detect and offer to remove the legacy `%LOCALAPPDATA%\Programs\PhoneMirror` program directory and `%LOCALAPPDATA%\PhoneMirror` state directory when either exists. Do not implement or backport it as part of the `1.16.0` release.

Before committing:

1. Run `build.ps1` and confirm compiler exit code 0.
2. Confirm `dist/` and the legacy `devices.local.txt` remain ignored, and that no `devices.json` exists inside the repository or package.
3. Search tracked files for real serials, private IPs, home-directory usernames, tokens, passwords, private keys, and generated binaries.
4. For transfer changes, validate both directions with a Unicode filename; for directory changes, include a nested Unicode folder and compare hashes.
5. For Camera/listing changes, verify a large directory displays only the first batch and “更多” remains enabled.
6. For lifecycle/input changes, run `OMirror.exe --stress-model <seed>` and, with only the target handset connected, `tests\Run-StabilityStress.ps1 -DeviceIndex 0 -DurationMinutes 10 -Seed <seed>`.
7. For every release after `1.16.0`, verify that `Uninstall.exe` is present beside `OMirror.exe` and exercise both data-retention choices before packaging.
8. For a release, package the complete `dist\OMirror` directory, calculate SHA-256, and provide concise release notes directly to the user for the GitHub form; do not maintain a separate release-notes file or commit the archive/bundled binaries.

## Change constraints

- Preserve Windows PowerShell 5.1 and .NET Framework compatibility; avoid APIs newer than the target framework unless verified by `csc.exe`.
- Keep ADB process I/O asynchronous enough to avoid stdout/stderr deadlocks.
- Never run the GUI elevated by default.
- Do not commit `dist/`, local device configuration, ADB logs, screenshots containing user data, or credentials.
- Update this file only with stable, verified facts. Put user-facing setup and feature summaries in `README.md`.
