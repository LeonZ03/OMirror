# AGENTS.md

This file is the compact source of truth for AI agents working in this repository. Read it before traversing code or binaries.

## Project identity

- Name: `OPhoneMirror`.
- Platform: Windows desktop, WinForms on .NET Framework.
- Current UI version: `1.12.1`; bundled runtime expected: scrcpy `4.1` with ADB `37.0.1`.
- There is intentionally no `.csproj`: `build.ps1` invokes the .NET Framework `csc.exe` directly.
- Repository source is self-contained. `dist/` and `devices.local.txt` are local-only and ignored.

## Source map

- `OPhoneMirror.cs`: shared Apple-inspired light/dark UI tokens and controls, owned device-picker popover, current-device state, scrcpy launch/stop/restart, keyboard mode persistence, and mode normalization.
- `TransferForm.cs`: themed two-pane PC/Android file browser, collapsible activity drawer, selection, transfer queue, collision confirmation, and large-directory batching.
- `AdbClient.cs`: quoted ADB execution, UTF-8 shell input, Unicode-safe file transfer, and tar-stream directory receive.
- `KeyboardCapture.cs`: low-level Windows keyboard hook active only when a tracked scrcpy window owns foreground focus.
- `app.manifest`: `asInvoker`; do not elevate, because Windows blocks Explorer drag/drop into elevated windows.
- `devices.example.txt`: public configuration template.
- `devices.local.txt`: real serials and labels; never commit.
- `build.ps1`: canonical build/package entry point.
- `OPhoneMirror.exe --self-test`: non-interactive package check; exit `0` means both device entries plus ADB/scrcpy were found, while exit `2` means configuration/runtime is incomplete.

## Configuration contract

`devices.local.txt` contains up to two non-comment lines:

```text
display name|model description|adb serial|window x
```

The executable reads this file beside itself. Missing/invalid entries become disabled “未配置设备” cards. Never restore hard-coded serials, IP addresses, usernames, or absolute user paths in tracked source.

Keyboard, screen-off, always-on-top, theme, and last-selected-device settings live under `%LOCALAPPDATA%\OPhoneMirror`; they are runtime state, not repository content.

## Verified implementation facts

- Mirror launch preset: USB serial, H.264, 60 fps, 16 Mbps, zero video buffer, no audio, 450×900 window. Do not pass scrcpy's `--always-on-top`; the persisted control-panel checkbox applies `HWND_TOPMOST` or `HWND_NOTOPMOST` after the window is created and hot-applies the same state without restarting scrcpy.
- The persisted “仅熄手机屏幕” setting is hot-applied through the one existing primary mirror only. It temporarily focuses that window and uses scrcpy's documented `MOD+O` for display-off. For display-on it issues a real right-click at the mirror center (SDL ignores posted background mouse events), then restores both cursor position and the previous foreground window. Never add `--turn-screen-off` to the launch arguments or start a second control-only scrcpy instance: multiple long-lived scrcpy servers proved unstable on the Reno6 USB transport. `KEYCODE_WAKEUP` is only a fallback for screen-on.
- Reno6 repeatedly entered ADB `offline` with the ADB `37.0.0` bundled by scrcpy 4.1, including while no OPhoneMirror/scrcpy process was running. Starting the separately installed Platform Tools ADB `37.0.1-15733141` immediately restored `device`. Release builds must therefore pass `-AdbDir` and bundle `adb.exe`, `AdbWinApi.dll`, and `AdbWinUsbApi.dll` from that tested runtime.
- Periodic ADB probes run on the thread pool behind an interlocked single-flight guard so a slow/offline USB transport cannot freeze the WinForms UI.
- The main window presents one active device. Clicking its selector opens a two-row device list; selection is persisted by index and updates the launch/transfer actions without changing the per-device scrcpy process state.
- The selector list is an owned borderless `DevicePickerForm`, anchored below the active-device row. It closes on deactivation or Escape and keeps the main settings layout stable.
- Theme mode persists as `0=auto`, `1=light`, or `2=dark`. Auto reads Windows `AppsUseLightTheme`; `SystemEvents.UserPreferenceChanged` hot-applies semantic colors to the main form, picker, open transfer forms, grids, and DWM title bars.
- The main action is stateful: idle `投屏`, starting `连接中`, running `停止`, and stopping `停止中`. Existing matching scrcpy processes are adopted instead of duplicated, and process exit updates the UI asynchronously.
- UI text uses `Microsoft YaHei UI` for crisp Chinese GDI rendering and `Segoe UI` only for the Latin product wordmark. Standard actions are vector-drawn icons with tooltips and accessible names; no emoji or bitmap icon font is required.
- `ModernButton` is a fully owner-drawn `Control`, not a native `Button`, and intentionally has no rounded Win32 `Region`. The single anti-aliased paint boundary avoids DPI-scaled corner fringes while the custom accessibility object preserves push-button semantics and keyboard activation.
- `app.manifest` declares `PerMonitorV2,PerMonitor` plus the legacy `true/pm` fallback. Keep the existing `AutoScaleMode.Dpi` forms so Windows renders text and vectors at the monitor's native scale instead of bitmap-stretching the whole window.
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
dist\OPhoneMirror\OPhoneMirror.exe
dist\OPhoneMirror\scrcpy\...
dist\OPhoneMirror\devices.local.txt  # only when local config exists
```

Before committing:

1. Run `build.ps1` and confirm compiler exit code 0.
2. Confirm `dist/` and `devices.local.txt` remain ignored.
3. Search tracked files for real serials, private IPs, home-directory usernames, tokens, passwords, private keys, and generated binaries.
4. For transfer changes, validate both directions with a Unicode filename; for directory changes, include a nested Unicode folder and compare hashes.
5. For Camera/listing changes, verify a large directory displays only the first batch and “更多” remains enabled.

## Change constraints

- Preserve Windows PowerShell 5.1 and .NET Framework compatibility; avoid APIs newer than the target framework unless verified by `csc.exe`.
- Keep ADB process I/O asynchronous enough to avoid stdout/stderr deadlocks.
- Never run the GUI elevated by default.
- Do not commit `dist/`, local device configuration, ADB logs, screenshots containing user data, or credentials.
- Update this file only with stable, verified facts. Put user-facing setup and feature summaries in `README.md`.
