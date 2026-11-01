# Windows keyboard and mouse reference

Default bindings implemented by the Windows frontend through Preview 0.12.0.
These are fixed application bindings; configurable keybinding maps are not yet
implemented. `Arrow` means one of Left, Right, Up or Down. Actions operate on the
active pane unless stated otherwise. This reference includes locally implemented
Preview 0.12.0 features; the corresponding source changes may not yet be published.

## Tabs and windows

| Binding | Action |
| --- | --- |
| Ctrl+Shift+T | New tab using the default shell profile |
| Ctrl+Shift+W | Close the active pane; closes its tab when it is the last pane |
| Ctrl+Shift+N | New terminal window |
| Ctrl+Shift+O | Choose a folder and open a new tab there |
| Ctrl+Alt+Shift+T | Reopen the most recently closed tab in this window, using fresh shells |
| Ctrl+Shift+R | Rename the current tab |
| Ctrl+Tab | Next tab, wrapping at the end |
| Ctrl+Shift+Tab | Previous tab, wrapping at the start |
| Alt+1 through Alt+9 | Select the corresponding tab, if present |
| Ctrl+Shift+PageUp | Move the current tab left |
| Ctrl+Shift+PageDown | Move the current tab right |
| Ctrl+Alt+PageUp | Activate the previous terminal window |
| Ctrl+Alt+PageDown | Activate the next terminal window |
| F11 | Toggle fullscreen |

Window cycling requires more than one terminal window and restores minimized
windows before activation. Windows controls whether an application can take focus.

The compact tab strip appears automatically with two or more tabs and disappears
when only one remains. **Ctrl+Shift+T** opens a second tab while the strip is
hidden; tab navigation and closing shortcuts continue to work.

## Split panes

| Binding | Action |
| --- | --- |
| Ctrl+Shift+D | Split right |
| Ctrl+Shift+E | Split down |
| Ctrl+Alt+Arrow | Focus the pane in that direction |
| Ctrl+Shift+Z | Toggle zoom of the active split |
| Ctrl+Alt+Shift+H | Move the nearest vertical divider left |
| Ctrl+Alt+Shift+J | Move the nearest horizontal divider down |
| Ctrl+Alt+Shift+K | Move the nearest horizontal divider up |
| Ctrl+Alt+Shift+L | Move the nearest vertical divider right |

Divider resizing requires a matching divider and is unavailable while a split is
zoomed. Each step changes the divider fraction by 5%, bounded to 5–95%.

## Text, selection and history

| Binding | Action |
| --- | --- |
| Ctrl+Shift+C | Copy selection |
| Ctrl+Shift+V | Paste |
| Shift+Insert | Paste |
| Ctrl+Shift+A | Select all, including scrollback |
| Ctrl+Alt+Shift+Arrow | Extend selection in that direction; starts at the cursor if there is no selection |
| Ctrl+Alt+Shift+Home / End | Extend selection to current line start/end |
| Ctrl+Alt+Home / End | Extend selection to screen start/last nonblank row |
| Ctrl+Alt+Shift+PageUp / PageDown | Extend selection by a page |
| Shift+PageUp | Scroll up one terminal page |
| Shift+PageDown | Scroll down one terminal page |
| Ctrl+Shift+Home | Scroll to the top of history |
| Ctrl+Shift+End | Scroll to the bottom |
| Ctrl+Shift+K | Clear screen, retaining scrollback |
| Ctrl+Shift+L | Toggle read-only mode |

Read-only mode blocks terminal input and paste while allowing selection and
scrolling. Ctrl+C remains terminal input rather than the application's copy
shortcut. Clipboard and selection behavior also follows the Settings preferences.

## Appearance and commands

| Binding | Action |
| --- | --- |
| Ctrl+Shift+P | Open command palette |
| Ctrl+Shift+F | Open terminal search |
| Ctrl+, | Open Settings |
| Ctrl+Shift+Plus | Increase font size |
| Ctrl+Shift+Minus | Decrease font size |
| Ctrl+Shift+0 | Reset font size |

Plus and Minus refer to the main keyboard OEM plus/equal and minus keys; numpad
Add/Subtract are not bound. Keyboard layout affects the physical keys used.

### Minimal interface: hiding and restoring the toolbar

| Keys / action | Result |
| --- | --- |
| Ctrl+Shift+P, search `Show toolbar`, then Enter | Toggle the top toolbar on or off |
| Ctrl+, | Open Settings, including when the toolbar is hidden |
| Settings → Show the top toolbar | Save the toolbar visibility preference |

`Show toolbar` is a command-palette action, without a dedicated single shortcut.
Its visibility preference applies immediately to open windows and persists across
launches. All keyboard shortcuts remain available with the toolbar hidden.

## Search, palette and dialogs

| Context | Binding | Action |
| --- | --- | --- |
| Search field | Enter | Find the next match |
| Search visible | Escape | Hide search and return focus to the terminal |
| Command palette query | Up / Down | Change selected command |
| Command palette query | Ctrl+Home / Ctrl+End | First / last command |
| Command palette query | Ctrl+PageUp / Ctrl+PageDown | Move eight commands up / down |
| Command palette | Enter | Run the selected available command |
| Command palette | Escape | Close the palette |
| Rename field | Enter | Save the tab name |
| Font-size / scroll-row dialogs | Escape | Close the dialog |

Other standard text-field editing and dialog navigation follow Windows/WPF
control behavior. Application shortcuts can take precedence over shell or TUI
bindings while the terminal has focus.

## Mouse gestures

| Gesture | Action |
| --- | --- |
| Left drag | Select cells |
| Double-click text | Select a word; drag to extend by words |
| Triple-click text | Select a line; drag to extend by lines |
| Alt+drag | Rectangular selection |
| Drag beyond the top/bottom edge | Autoscroll during selection |
| Ctrl+click a detected HTTP(S) or OSC 8 link | Open the link |
| Mouse wheel | Scroll history using the configured wheel speed |
| Ctrl+mouse wheel | Increase/decrease font size |
| Shift with mouse gestures | Override application mouse reporting for local handling |
| Right-click | Configured action; default is context menu |
| Middle-click | Configured action; default is paste |
| Double-click tab header | Rename the tab |

Without a local override, mouse-reporting terminal applications can receive mouse
events instead of local selection/scroll actions. Right/middle-click actions can
be set to context menu, paste, copy, copy-or-paste or none in Settings.

## Actions without dedicated shortcuts

Use **Ctrl+Shift+P**, then search for the action name. Commands include terminal
reset; application mouse-reporting toggle; exact font size; copying the tab title;
screen, selection, scrollback and styled HTML exports; searching selected text;
equalizing splits; showing/hiding the toolbar; always-on-top; decorations; resetting window size;
focus-follows-mouse; named shell-profile tabs; saving the workspace; scrolling to
selection or a row; previous/next shell prompt; and opening, reloading, resetting,
exporting or importing preferences.

Prompt navigation requires OSC 133 prompt markers supplied by the shell. The
palette shows command availability and toggle state; unavailable actions cannot
run.

## Implementation references

Bindings were checked against [MainWindow.cs](windows/MainWindow.cs),
[WindowCommands.cs](windows/WindowCommands.cs),
[MoreCommands.cs](windows/MoreCommands.cs), and
[TerminalView.cs](windows/TerminalView.cs). See the
[Windows README](windows/README.md) for setup and feature details.

Preview 0.9.0 adds palette actions **Scroll up/down by configured lines** and
**Scroll up/down by configured page fraction**. Configure their amounts in
Settings → Cursor, typography, themes and scrolling. They have no dedicated
shortcut; Shift+PageUp/PageDown continues to scroll by a whole page.

Preview 0.10.0 palette actions add HTML selection/screen export, ANSI export for
screen/selection/scrollback, formatted HTML selection copy, visible-screen/history
copy, single-line paste, tab overview, tab duplication and closed-tab reopening.
Duplicate/reopen start fresh processes. Closing the last window discards reopen
recipes. Tab presentation and clipboard policies live in Settings → Appearance,
scrolling, clipboard and tabs.

Preview 0.11.0 adds palette actions **Open working directory in Explorer**,
**Copy working directory**, and **Open Windows on-screen keyboard**, accessed
through Ctrl+Shift+P. No dedicated shortcuts are assigned to these actions.

Preview 0.12.0 adds palette actions for selecting/clearing selection, numbered tab
navigation, close tab/window, welcome preview, primary-selection copy/paste/clear
and **Copy last hovered link address**. Existing primary shortcuts are unchanged;
labels and dispatch are now generated by one chord owner. Primary selection is a
separate Ghostty-local memory buffer; it does not replace the Windows clipboard.

Windows owns reserved combinations such as Alt+Tab, Win+Tab and Win+L; Ghostty
handles only keys Windows delivers to its focused window. No system-wide keyboard
hook, global shortcut registration or registry override is installed. A future
global/drop-down shortcut must be opt-in, use RegisterHotKey and report conflicts
instead of stealing an existing binding. User-defined remaps/global key tables
remain outstanding. OSK is a Windows accessibility application, with Windows-owned
permissions and behavior.


## Custom bindings (Preview 0.13)

Use **Open settings JSON** in the command palette. `KeyBindings` accepts single
key chords, checked for duplicates on save/reload. Modifier names: Ctrl/Control,
Alt, Shift, Win/Windows. Use `Plus`, `Minus`, `Comma` for punctuation and WPF key
names such as `F12`, `PageUp`, `OemPeriod`. `Ctrl++` is invalid; use `Ctrl+Plus`.
Windows may reserve a chord before the application receives it.

```json
{
  "ShortcutPrecedence": "Application",
  "KeyBindings": [
    { "Chord": "Ctrl+F12", "Action": "new-tab-default" },
    { "Chord": "Ctrl+Shift+C", "Action": "unbind" },
    { "Chord": "Alt+F12", "Action": "send-text", "Value": "hello" },
    { "Chord": "Ctrl+F10", "Action": "send-csi", "Value": "5~" },
    { "Chord": "Ctrl+F9", "Action": "send-escape", "Value": "a" },
    { "Chord": "Ctrl+F8", "Action": "send-cursor", "Value": "Left" }
  ]
}
```

Merge these fields into your existing JSON. Named action IDs come from **Open
terminal inspector**, which also shows their current availability. Send-text
encodes UTF-8; CSI prefixes ESC `[`, escape prefixes ESC, cursor actions use the
native key encoder (Left/Right/Up/Down/Home/End), respecting application-cursor
and negotiated keyboard modes. Sending respects read-only panes and intro skip.

Custom bindings apply while a terminal pane has keyboard focus and precede default
application shortcuts. `ignore` consumes the chord; `unbind` forwards it to the
terminal, including chords otherwise handled by terminal copy/paste fallbacks.
Named actions honor their existing availability checks; unknown IDs show a notice.
A custom named binding adds an alternative; unbind the old chord to remap it fully.
Palette labels reflect effective bindings. `ShortcutPrecedence: "Terminal"`
forwards default application chords; custom bindings still run first.
**Ctrl+,** and **Ctrl+Shift+P** remain recovery shortcuts and cannot be replaced.
Search boxes retain normal text editing and Escape closes search.

Key sequences, action chains, key tables, global/all-surface and performable
semantics remain outstanding. [Action inventory](windows/ACTION_SUPPORT.md)
compares the pinned upstream action surface with Windows equivalents.

The palette also has **Open terminal inspector** and **Export diagnostics JSON**.
They have no default shortcut; assign their IDs `terminal-inspector` and
`export-diagnostics` if desired.
