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

Preview **0.8.0** adds another 20 features, listed separately in [TODO.md](../TODO.md).
Commands now include nearest-divider resizing, window cycling, scroll-to-selection,
absolute row navigation, previous/next shell prompts, arrow-key selection extension,
styled HTML export, and open/reload/reset/import/export of Windows JSON preferences.
Prompt navigation requires OSC 133 markers from the shell; shell integration is
not installed automatically. Row offsets start at zero from retained history.
Divider resizing moves the nearest matching divider five percentage points and
clamps it to 5–95%; zoomed splits must be unzoomed first.

Settings adds wheel lines per notch (including zero to disable local scrolling),
pointer hiding while typing, right/middle-click policies and selection clearing
after copy or on typing/paste/drop. Precision wheel deltas accumulate rather than
jumping to the bottom when too small to produce a line. Application mouse reporting
still takes precedence unless overridden locally.

Scrollback and native image-storage quotas are configurable as exact byte counts.
Blank means unlimited; zero disables history or Kitty graphics and discards retained
data. An omitted JSON option defaults to 64 MiB (67108864 bytes). Limits are applied
live; scrollback enforcement is page-granular and the active screen remains allocated.
Per-image decoding safeguards and managed image-cache behavior remain separate.

Preference exports exclude workspace state. Import/reset keep the current live
workspace, retain unknown settings and back up the previous destination file.
Import replaces known preferences with values/defaults from the selected JSON file;
reload reads the owned settings file without restarting shells. Open settings JSON
uses Notepad. These commands do not implement Ghostty's upstream text config format
or automatic file watching. Native picker/editor and clipboard interaction still
require manual verification.

Preview **0.7.0** adds the 20-feature batch recorded in [TODO.md](../TODO.md).
Open **Commands** / **Ctrl+Shift+P** to find all actions, their availability,
keyboard shortcuts and current toggle state. Arrow keys navigate results while
typing filters them; Enter runs the selected action and Escape dismisses it.

New terminal actions include reset, clear screen, read-only mode, a local
mouse-reporting override, font reset/set-size, history top/bottom, copy tab title,
search selected text, and separate visible-screen/selection exports. Exports
are plain text. Clear screen preserves history and terminal modes; reset clears
history and modes. Neither restarts the shell. Read-only blocks user keyboard,
paste, drop and mouse input while allowing terminal output, protocol replies,
selection, copy and local scrolling. Disabling application mouse reporting
keeps mouse interactions local without rewriting the application's requested
terminal mode.

Tabs can be reordered by keyboard. Split zoom is temporary and preserves the
saved tree; navigating away unzooms it. Equalization gives every leaf pane equal
area, including mixed split orientations. Window actions add always-on-top,
decoration visibility and reset-size, and fullscreen now restores the preceding
window state/bounds/frame. Focus-follows-mouse is optional in Settings and only
focuses panes inside the active window when there is no mouse capture.

Font zoom/set-size updates every pane/window sharing settings. Reset returns
to the size loaded at startup or last applied through Settings. Mode toggles,
zoom state, topmost and decoration state apply to live sessions/windows;
workspace restoration does not persist them yet.

| Action | Shortcut |
| --- | --- |
| New tab / close pane | Ctrl+Shift+T / Ctrl+Shift+W |
| Split right / down | Ctrl+Shift+D / Ctrl+Shift+E |
| New window | Ctrl+Shift+N |
| New tab in folder | Ctrl+Shift+O |
| Rename tab | Ctrl+Shift+R, or double-click tab header |
| Search / command palette | Ctrl+Shift+F / Ctrl+Shift+P |
| Copy / paste | Ctrl+Shift+C / Ctrl+Shift+V |
| Clear screen / toggle read-only | Ctrl+Shift+K / Ctrl+Shift+L |
| Reset font size | Ctrl+Shift+0 |
| Top / bottom of scrollback | Ctrl+Shift+Home / Ctrl+Shift+End |
| Move tab left / right | Ctrl+Shift+PageUp / Ctrl+Shift+PageDown |
| Zoom / unzoom active split | Ctrl+Shift+Z |
| Move nearest split divider left/down/up/right | Ctrl+Alt+Shift+H/J/K/L |
| Previous / next terminal window | Ctrl+Alt+PageUp / Ctrl+Alt+PageDown |
| Extend selection | Ctrl+Alt+Shift+arrow |
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

Preview 0.6.3 refines the welcome graphic to match the icon's rounded ghost,
prompt-like face and scalloped hem, with a consistently spaced GHOSTTY wordmark.

Preview 0.6.2 prints a cyan-to-violet Ghostty ASCII welcome graphic in new
PowerShell, CMD and WSL sessions. Narrow panes use compact art; panes narrower
than 16 columns skip it. Disable **Show gradient Ghostty welcome art in new
sessions** in Settings for a plain startup. Existing sessions are unaffected.
The banner is printed through the shell session, not injected into terminal
input or installed into your shell profile. User startup scripts may clear it.
The CMD banner uses Windows PowerShell briefly to print VT colors; restricted
PowerShell environments may require disabling the welcome option.

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

Hide the top button row in **Settings → Show the top toolbar**. This applies
immediately to open windows and is saved for future launches. The tab strip
appears only when two or more tabs are open. Use **Ctrl+Shift+P → Show toolbar** to toggle it back on, or
**Ctrl+,** to open Settings while it is hidden.

The compact tab strip appears automatically with two or more tabs and collapses
when only one remains. Ctrl+Shift+T still opens another tab while it is hidden.

### Acrylic background

In Settings, choose **Background effect → Acrylic** and adjust **Background
opacity** (0–100%). Apply/Save updates open windows without restarting shells.
Lower values reveal more of Windows' frosted backdrop; 100% covers it with the
terminal background color. Text, cursor, selections and explicit application
background colors remain opaque. The default is Solid.

Acrylic requires Windows 11 22H2 (build 22621) or later with Windows Transparency
effects enabled. High contrast, unsupported systems and native API failures use
a solid background. Windows may substitute its own fallback material under
power-saving or remote-session conditions. The slider adjusts the terminal tint,
not the system blur radius; fully clear, unblurred transparency and independent
blur strength are not implemented.

### Animated ghost experiment

Open **Settings → Preview animated ASCII ghost...** to try a procedural shaded
ASCII ghost making one rotation, then cycling through chevron, literal @ and
terminal-style eyes while stationary before repeating. Eye masks use black
foreground characters rather than spaces. The entire ghost gently bobs up and
down throughout the loop; the caption stays stationary.
Use Replay sequence to repeat it. This isolated preview does not start a shell
or change the normal welcome banner. It is inspired by the homepage animation,
not a copy of its artwork or animation frames.

Enable **Settings → Animate welcome ghost** to play one bouncing rotation and
eye sequence before each new shell starts. Press any key to skip the intro; the
skip key is consumed. The normal static banner follows, and the animation stops
before the prompt so command output cannot be overwritten. Requires Show welcome
art; narrow panes use the static banner. Existing running sessions are unaffected.
The animation option defaults off.

### Preview 0.9.0: cursor, typography, themes and scrolling

Expand **Settings → Cursor, typography, themes and scrolling** for cursor shape,
color, opacity, blink and unfocused appearance; selection/search colors; inactive
pane content opacity; cell width/height multipliers; horizontal/vertical padding;
automatic light/dark terminal themes; and separate installed bold/italic families.
All apply to open panes when saved. Blank font overrides inherit the regular font;
bold takes priority for combined bold-italic text. Cell scaling changes the grid,
not font size or shaping. Small cells can cause glyph overhang. Cursor hiding by
applications is respected. Blink timing is the half-period in milliseconds.

Palette commands scroll up/down by the configured line count or page fraction.
Selection adds Ctrl+Alt+Shift+Home/End (line boundaries), Ctrl+Alt+Home/End (screen
boundaries), and Ctrl+Alt+Shift+PageUp/PageDown (page extension). The native engine
retains the selection anchor while extending the active end. See KEYBINDS.md in
the repository and TODO.md for the exact twenty-feature batch and remaining work.

Wheel scrolling uses two terminal lines per notch by default. Scrollable UI
controls use 24 DIPs per notch or one logical item, preserving fractional input
instead of treating every small touchpad event as a full notch. Application mouse
reporting remains controlled by the terminal application. Ctrl+wheel font zoom is
unchanged.

### Preview 0.10.0: clipboard, exports and tabs

Expand **Settings → Appearance, scrolling, clipboard and tabs** for copied trailing
whitespace and Windows/LF line endings, open-after-export, tab visibility
(Automatic/Always/Never), Top/Bottom placement, Unicode-safe title length, close
buttons, optional middle-click close and a dedicated bold-italic font family.

Ctrl+Shift+P exposes HTML selection/visible-screen exports, ANSI screen/selection/
scrollback exports, formatted HTML selection copy, plain screen/history copy,
single-line paste, tab overview and fresh-session tab duplication. HTML includes
native palette CSS. Formatted copy includes plain text for receiving applications
that do not support HTML; CSS fidelity depends on that application. Open-after-
export uses the default Windows file association and is disabled by default.
Single-line paste replaces line breaks with spaces; it does not escape shell
metacharacters. Existing multiline-paste protection remains in effect for normal
paste.

Ctrl+Alt+Shift+T reopens a closed tab's layout using fresh shells. Up to ten recipes
are retained per open window; they are lost when the window closes. Layout, names
and supported working directories are copied; process state, output and zoom are
not restored. Duplicate/reopen enforce the existing tab/pane capacity limits.
In the command-palette query, Ctrl+Home/End selects first/last and
Ctrl+PageUp/PageDown moves eight commands. Unmodified text-editing keys keep their
normal behavior.

### Preview 0.11.0: launch options, background images and live reload

Examples (PowerShell):

```powershell
& .\Ghostty.Windows.exe --profile "Command Prompt" --directory C:\src --title "Build" --width 1100 --height 700
& .\Ghostty.Windows.exe --command '"C:\Windows\System32\cmd.exe" /Q /K' --maximize
& .\Ghostty.Windows.exe --fullscreen
& .\Ghostty.Windows.exe --help
& .\Ghostty.Windows.exe --diagnostics
```

Help/diagnostics display local GUI windows. Explicit launch options open one fresh
tab and do not overwrite saved preferences/workspace on close. --command passes a
Windows executable/argument command line directly to CreateProcess, so shell
syntax needs an explicit shell executable. Custom commands skip the welcome
bootstrap and are not saved as editable profiles or restored process recipes.
Without launch arguments, the saved workspace behavior is unchanged.

In **Settings → Appearance, scrolling, clipboard and tabs**, choose a background
image or enter its absolute path. Control opacity, Cover/Contain/Stretch/Center,
horizontal alignment and tiling. Sources are limited to 64 MiB and 32 million
pixels, converted to BGRA32, cached per pane, and do not retain an open file handle.
Only the first frame is displayed. Missing/unreadable images fall back to the
normal background with a nonfatal notice. Image file changes are checked on
Settings Apply/reload; image files are not continuously watched.

Automatic settings reload is opt-in with a 100–5000 ms debounce. One watcher serves
windows sharing a settings owner. Invalid/partial external edits leave live
preferences and running sessions intact and display a dismissible notice. Fixing
the JSON clears the reload error. Reload preserves the live workspace; there is
no concurrent-edit merge or upstream text-config support. Palette actions can
open/copy the active supported working directory or launch the Windows on-screen
keyboard. See AUDIT.md for cleanup scope and remaining P0 limitations.

### Preview 0.12.0: completing partially implemented areas

This pass completes 15 partial checklist entries; 47 remain. Expand the Settings
appearance section for custom Unicode word-boundary characters and repeat-click
interval/distance (zero follows Windows), configurable Shift mouse override,
vertical background alignment, glyph baseline and decoration thickness, balanced
cell padding, palette-based contrast, block-cursor text color, application-driven
cursor blink, primary selection and per-window tab/pane limits.

Paste now has explicit **Reject** or **Strip** control-character policies. Reject
sends nothing and reports why. Strip reports how many controls it removed.
Multiline confirmation can remain enabled for bracketed-paste applications too.
Single-line paste normalizes line breaks without quoting shell syntax.

The optional **primary selection** buffer is local to this Ghostty process, shared
by its panes/windows, bounded to 8 MiB and never saved. It is separate from the
Windows clipboard. Enable automatic mouse-selection capture and choose **Paste
primary selection** for middle-click, or use palette copy/paste/clear actions.
Regular Windows clipboard copying/pasting continues to work independently.

File drops insert standalone arguments using PowerShell literal quoting, CMD
quoting, or POSIX quoting after the default WSL distribution's wslpath translation.
CMD filenames containing percent/exclamation expansion markers are refused rather
than rewritten. WSL quoting targets POSIX-compatible shells; distribution-specific
working-directory inheritance remains a separate outstanding feature. Unknown
custom-command shells and failed translations are refused with visible feedback.

Palette labels and shortcut dispatch now share one chord model, including terminal
chords such as Shift+Insert. Enabled/toggle state refreshes while the palette is
open. New actions cover select/clear selection, tab navigation, search dismissal,
window/tab close, welcome preview and copying the last hovered link address.
Hover hints refresh with output, scrolling/layout and modifier changes.

Native startup checks ABI version, structure sizes, pointer width and capabilities
before registering callbacks. EXE/managed/native DLLs must come from one matching
build. Invalid configured fonts/themes/profiles are diagnosed instead of silently
replaced. Only first-run defaults choose an installed preferred family. Native
formatter failures preserve invalid-input/allocation/capacity categories.

Managed rendering/measurement failures and failed shell starts stay within their
pane with recovery feedback. Correct settings or reset/close that pane. This does
not promise recovery from unsafe native memory faults or process-wide exhaustion.
Minimum contrast is best-achievable against terminal cell colors, not the unknown
physical desktop under Acrylic. Configured limits affect new surfaces; existing
restored sessions are retained. See TODO.md and AUDIT.md for remaining scope.


## Preview 0.13: bindings and diagnostics

Custom single-chord `KeyBindings` and `ShortcutPrecedence` are documented in
[KEYBINDS.md](../KEYBINDS.md). The command palette opens a terminal inspector and
exports a local diagnostics JSON report. The inspector shows cursor/modes,
geometry, images, native-feed timing/byte counts, accepted-input and queued-byte
counts, glyph/brush cache counts, unsupported JSON preference keys and command
IDs/availability. These are pane-level counters, not a CPU/GPU profiler or native
allocator accounting. Refresh is explicit so inspecting an idle pane does not
continuously allocate snapshots.

Settings has an opt-in **Capture bounded unsupported OSC/APC samples** control.
It keeps at most 32 samples of 256 bytes per pane in RAM; the inspector reports
truncation/evictions and can display Base64. Disabling capture erases retained
samples. Default exports omit payload bytes and never include screen text,
typed-input contents, environment variables or settings values. They include
artifact SHA-256 identities, ABI metadata, platform/runtime and current pane state.
Recognized-but-disabled protocols, cancelled sequences, malformed known sequences
and general CSI/DCS traffic are not covered by this unknown-sequence callback.
No unknown payload is executed, used as a command, or interpreted as host authority.

`Ghostty.Windows.exe --diagnostics-file PATH` exports platform/artifact diagnostics
without opening a shell and can report an unavailable/mismatched native ABI.
Replacing an existing report preserves it as `.backup`; GUI export uses a save
picker. Reports stay local. Managed snapshot failures are included as unavailable
state while preserving other diagnostic facts.

The host replies to ENQ and XTVERSION with `ghostty-windows VERSION`; child
processes also get `TERM_PROGRAM_VERSION`. Color-scheme queries reflect terminal
background luminance (below 0.5 = dark). Mode 2031 enables theme-change reports;
unchanged themes and disabled notifications produce no unsolicited reports.
Auto terminal-theme switching consequently follows Windows dark/light changes.
DA1 retains the pinned engine's conservative VT220/ANSI-color claim (`62;22`),
without claiming Sixel or OSC52. DA2/DA3 retain upstream defaults. WSL uses
`TERM=xterm-256color`; a dedicated Ghostty terminfo installation remains outstanding.

The complete pinned inventories cover [219 configuration fields](CONFIG_SUPPORT.md)
and [88 actions](ACTION_SUPPORT.md). They explicitly distinguish Windows
JSON/command equivalents from unsupported upstream syntax and behavior.


## Verification coverage

[The 28-case verification matrix](VERIFICATION.md) records real evidence, known
gaps and remaining device/OS/reference-terminal requirements. Run
`windows/verify.ps1` for the full regression suite, bounded stress/application
cases and the production compression probe. This is not certification of all
28 items; physical, cross-machine and long-duration coverage remains open.
