# Ghostty Windows frontend — experimental

An independent native Windows frontend built against the real upstream
libghostty-vt engine. This is an experimental port, not an official Ghostty
Windows release and not yet feature equivalent to Ghostty's macOS/Linux GUI.

Upstream source: https://github.com/ghostty-org/ghostty

## Build and run

Requires Windows 10 1809+ x64, Zig 0.16.0 and .NET SDK 8. The script uses
user-local tool installations by default; override `-Zig` and `-Dotnet` as needed.

```powershell
.\windows\build.ps1 -Test
.\windows\dist\Ghostty.Windows.exe
```

The published folder includes its .NET runtime; end users do not need the SDK.
Copy the entire `dist` folder together, not just its EXE.

## Implemented

- Ghostty VT engine, screen state, ANSI/24-bit colors, alternate screen,
  Unicode graphemes, reflow, scrollback and terminal device responses.
- Windows ConPTY with PowerShell, Command Prompt and WSL profiles.
- Tabs, new windows, nested resizable panes and close confirmation.
- Native WPF rendering, font fallback, bold, italic, underline and strikeout.
- Mouse selection, copy, paste, scrollback, font zoom and text export.
- Ghostty keyboard encoding for normal/application cursor and keypad modes,
  F1–F24, physical scan-code identity and layout-dependent Unicode text.
- Negotiated Kitty keyboard presses, repeats, releases, shifted/base alternates,
  associated text, modifier sides and lock state; pane focus-loss cleanup.
- Mode-aware bracketed paste with multiline confirmation.
- Mouse protocol reporting with Shift override for selection, focus reporting,
  synchronized output with a one-second timeout, and Ctrl+click OSC 8 HTTP(S) links.
- Search including scrollback, theme/font settings and command palette.
- Workspace restoration: nested split layouts, pane proportions, shell profiles,
  launch folders, custom tab names, selected tab/pane, window size and maximized state.
- Named tabs (double-click the tab header or Ctrl+Shift+R) and launching tabs
  in a selected folder (Shell menu or Ctrl+Shift+O).
- Job Object process ownership: closing a session terminates its process tree.
- Inline Kitty PNG/RGB/RGBA images, chunked and zlib-compressed input,
  alpha, cropping, pixel offsets, cell sizing, z-layers, replacement and deletion.
  Immutable generation-keyed image snapshots respect synchronized output.
- Multi-resolution Ghostty application icon in the executable, window and shortcut.
- Native title bars follow Windows' app light/dark preference at startup and
  when it changes, including Settings and Commands windows.

## Shortcuts

| Action | Shortcut |
| --- | --- |
| New tab / close pane | Ctrl+Shift+T / Ctrl+Shift+W |
| Split right / down | Ctrl+Shift+D / Ctrl+Shift+E |
| New window | Ctrl+Shift+N |
| New tab in folder | Ctrl+Shift+O |
| Rename tab | Ctrl+Shift+R, or double-click tab header |
| Search / command palette | Ctrl+Shift+F / Ctrl+Shift+P |
| Copy / paste | Ctrl+Shift+C / Ctrl+Shift+V |
| Select all, including history | Ctrl+Shift+A |
| Settings | Ctrl+, |
| Cycle tabs | Ctrl+Tab |
| Previous tab | Ctrl+Shift+Tab |
| Switch split pane | Ctrl+Alt+arrow |
| Fullscreen | F11 |
| Font zoom | Ctrl+mouse wheel |
| Scroll history | Mouse wheel, Shift+PageUp/PageDown |

Settings live in `%LOCALAPPDATA%\Ghostty.Windows\settings.json`. Invalid JSON
produces an actionable startup error rather than overwriting the user's file.
The folder also holds startup/render error logs.

## Selection and links

Preview 0.6 uses Ghostty's native selection gestures: double-click selects a
word, triple-click selects a logical line including its soft-wrapped rows,
and dragging after either gesture extends by words or lines. Hold **Alt**
while dragging for rectangular selection. Drag beyond the top/bottom edge to
scroll through history. Resize cancels an active drag. **Ctrl+Shift+A** selects
all terminal text, including scrollback.

Right-click opens Copy/Paste/Select All and, over a web link, Open Link and
Copy Link Address. Mouse-reporting applications retain their mouse events;
hold **Shift** to override reporting for local selection or the menu.
Middle-click paste remains available.

HTTP/HTTPS links are detected from the visible terminal snapshot, with native
soft-wrap and wide-cell metadata. Ctrl+click opens a link; hover shows its
destination. Explicit OSC 8 destinations take precedence over visible text.
Detection skips hidden text and links cut off by the viewport. Links cannot
activate against a held synchronized frame. Non-web links and file-path
launching are not implemented. Punctuation detection is heuristic; OSC 8
preserves explicit destinations without those heuristics.

## Workspaces

Enable **Restore tabs and split layouts on next launch** in Settings. Closing
a window saves its workspace; **Save workspace now** in Commands saves it
without closing. The most recently saved window is restored on the next app
launch. Creating a new window starts a fresh tab.

Restoration starts new shells. It does not resume old commands, processes,
scrollback, or image state. Splitting an existing live pane preserves that
pane's running shell. Launch folders inherit from the active pane when a local
Windows folder is reported through OSC 7, otherwise from its original launch
folder. Linux working-directory reports are not mapped to Windows paths.
Missing folders remain in the saved layout and produce a launch error in that
pane; they are not silently replaced.

The previous flat tab list migrates automatically. Settings saves are atomic,
keep the previous file as `settings.json.backup`, and preserve unrecognized
settings. Unsupported or invalid workspace schemas produce an error without
overwriting the file. Restoration is bounded to 20 tabs, 64 panes and 24 levels
of nested splits per window.

## Settings

Preview 0.6.1 uses shared dark styles for application menus, dropdowns,
editable fields, buttons, checkboxes, scrollbars, tooltips, command lists and
confirmation/error dialogs. Terminal color themes remain separate. Native
title bars and Windows-owned file/folder pickers follow Windows preferences.

Open **Settings** from the toolbar or press **Ctrl+,**. Preview 0.5 adds a
resizable, scrollable editor with fixed Save/Apply/Cancel buttons. Apply saves
and updates open panes; Save also closes the editor. Cancel discards edits
since the last Apply. Invalid input and write failures appear inside the
editor and do not partially change live settings. Saves retain the prior file
as a backup.

Choose an installed font or enter a family name, adjust its size, select a
terminal theme, choose the default shell and browse for a startup folder.
Shell/folder changes affect new sessions; existing shells keep running, and
splits inherit their source pane's folder. Workspace restoration remains
separate from the default shell/folder choices.

Additional preferences control copy-on-select, multiline-paste confirmation,
close confirmation and status-bar visibility. Copy-on-select is off by
default; paste and close confirmation remain enabled by default. Appearance
and status-bar updates apply to all open windows sharing the settings.

## Keyboard input

Preview 0.4 supplies Windows press/repeat/release state to Ghostty's native
encoder. Applications opt into Kitty keyboard reporting themselves; legacy
shells continue to receive legacy input. Numeric keypad Enter and navigation
remain distinct from the main keyboard. Application-keypad behavior follows
Ghostty's modes, including its default mode 1035 override.

WPF supplies committed layout text. AltGr's synthetic Ctrl+Right Alt pair is
consumed for text input, and multi-character IME commits stay intact even when
an application requests Kitty report-all. Focus changes release keys that were
forwarded by that pane; releases belonging to application shortcuts are ignored.
Input larger than the previous 4096-byte encoder buffer is sized dynamically.

Automated checks cover a real Windows message pump and a real WSL raw-input
application receiving exact Kitty event bytes through ConPTY. AltGr and
non-US layout cases are controlled event fixtures, not physical keyboard
validation across locales. Full IME candidate positioning and accessibility
remain outstanding.

## Remaining work before full parity

See the [comprehensive Windows backlog](../TODO.md) for missing features,
partial integrations, verification work and release criteria.

This frontend is not a completed full-featured port. In particular:

- Kitty Unicode-placeholder placements, animation playback and external file/shared-memory
  image transports remain unsupported or unverified. There is no custom shader pipeline.
- No font ligature/shaping pipeline across cells. WPF font fallback renders
  individual grapheme clusters, not runs shaped by Ghostty's renderer.
- IME positioning and end-to-end physical-keyboard validation of AltGr,
  dead keys, non-US layouts and composition cancellation remain outstanding.
  Complete Kitty interoperability across third-party applications is unverified.
- No OSC clipboard integration, native notifications,
  upstream Ghostty configuration parser, shell integration installer, or quick terminal.
- No accessibility text provider, installer, signing, auto-update or crash recovery.
- Rendering performance, high-output backpressure and shutdown races need stress
  testing before daily use. Search runs synchronously under the terminal lock.

The smoke suite launches real shells and checks the C ABI adapter. Its report
is `windows/artifacts/result.json`; `preview.png` is a rendered UI capture.
`kitty-render.png` captures a protocol-driven image placement. Test reports hash
the executable, managed frontend and native adapter to identify the tested build.
Passing this suite does not establish full Ghostty feature parity.

PNG decoding uses Windows' imaging stack and the terminal's native allocator.
Native image storage is bounded to 64 MiB per terminal. Managed render snapshots
are cached by image generation and released when no longer visible; converting
gray/RGB input to BGRA can expand its representation by up to four times.

All upstream MIT notices are retained. New frontend code is under the same MIT license.
