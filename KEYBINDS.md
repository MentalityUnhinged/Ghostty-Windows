# Windows keyboard and mouse reference

Default bindings implemented by the Windows frontend through Preview 0.8.0.
These are fixed application bindings; configurable keybinding maps are not yet
implemented. `Arrow` means one of Left, Right, Up or Down. Actions operate on the
active pane unless stated otherwise. This reference includes locally implemented
Preview 0.8.0 features; the corresponding source changes may not yet be published.

## Tabs and windows

| Binding | Action |
| --- | --- |
| Ctrl+Shift+T | New tab using the default shell profile |
| Ctrl+Shift+W | Close the active pane; closes its tab when it is the last pane |
| Ctrl+Shift+N | New terminal window |
| Ctrl+Shift+O | Choose a folder and open a new tab there |
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
