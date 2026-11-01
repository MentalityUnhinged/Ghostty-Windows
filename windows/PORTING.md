# Port status and verification

The Windows frontend was added on top of upstream commit
`d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`. The complete upstream engine remains
unchanged. Only `windows/` is new. The repository is on `codex/windows-terminal`.

## Architecture

`ConPTY read pump → Engine lock → libghostty-vt → render snapshot → WPF`

Input runs in the opposite direction through Ghostty's key/mouse encoders and
a bounded ConPTY writer queue. Each session has an unnamed kill-on-close Job
Object. Shells start suspended and enter the job before resuming. The native
adapter uses the upstream C headers, then exposes a small frozen-layout ABI to
C#. Borrowed native pointers are consumed while the Engine lock is held.

Selection export re-reads the engine's current tracked selection after writes;
it does not retain stale grid references. Synchronized output snapshots the
completed frame in the engine callback and times out after one second.
The same callback captures immutable image placements and pixels at the hold
boundary. Native borrowed image handles never escape the terminal lock. Image
cache keys use the engine's generation stamp, so same-ID retransmissions and
screen switches cannot reuse stale pixels. PNG decoding runs on the I/O thread
and allocates successful output through Ghostty's supplied allocator.

The renderer uses WPF's drawing pipeline and font fallback. It is a new
renderer, not a port of Ghostty's Metal/OpenGL renderer, and equivalent GPU
performance or typography is not established.

`Workspace.cs` owns the typed pane tree. UI construction, split edits and
serialization consume the same tree instead of reinterpreting the visual
hierarchy. Rebuilding split containers reuses existing terminal views and their
ConPTY owners. Focus restoration brackets WPF's automatic focus moves during
tab changes and layout edits, preserving each tab's selected pane.

`WindowsKeyboard` observes window-thread key messages without consuming them,
preserves the extended scan-code prefix and queries unshifted text through
`ToUnicodeEx` with its non-mutating flag. WPF owns committed text and preedit.
`TerminalKeyboard` owns each pane's forwarded keys, correlates text commits,
and releases held keys on focus loss. The bridge resets every native event
field on each call and leaves escape-sequence generation to libghostty-vt.
AltGr's Windows synthetic modifier pair is normalized at the input boundary;
the native legacy C0 encoder otherwise considers it a Ctrl shortcut.

## Verification boundaries

`WindowCommands.cs` owns the named command catalogue, availability and shortcut
metadata used by the palette and key routing. Live font/configuration changes
are published by the Settings owner; panes and windows unsubscribe when closed.
Settings drafts do not inherit live event subscriptions. Split zoom changes the
visual tree while retaining canonical layout proportions and terminal owners;
tab reorder changes both UI and serialization order. Read-only mode gates user
input and releases tracked keys/buttons on entry; engine replies remain active.

`Chrome.xaml` owns application control styles, including popup templates and
menu separator resources. Settings no longer overrides them with light fields.
`Dialogs` supplies themed application confirmations/errors; native file/folder
pickers and DWM title bars remain Windows-owned surfaces.

Selection gestures are owned by the native adapter and freed before their
terminal. Reusable events receive fresh grid references on every operation;
only Ghostty's gesture and terminal selection retain tracked anchors. The
adapter no longer stores a duplicate selection snapshot or selected flag.
Resize and explicit selection operations reset gesture ownership. URL hit
testing consumes immutable render snapshots, including native wrap/continuation
and wide-cell spacer facts, and preserves explicit OSC 8 targets.

`SettingsWindow` edits a draft and keeps its action footer outside the scrolling
fields. The owning Settings object persists a candidate atomically before
copying its editable values into live state. MainWindow then refreshes all
windows sharing that owner. IO failures remain inline and leave live options
unchanged. Shell and startup-folder options are consumed only by new sessions.

- `App.cs` self-tests cover real shell startup, ConPTY input/output, resize,
  VT colors, Unicode, scrollback, keyboard modes, mouse encoding, focus,
  synchronized output, tracked selection, OSC hyperlinks, and window layout.
- `GraphicsTests.cs` sends real Kitty sequences through the compiled engine,
  exercises the real Windows PNG decoder, and verifies pixels from the
  production WPF renderer, including z-layers and high-DPI cell geometry.
- `WorkspaceTests.cs` saves and reopens actual windows, checks split proportions,
  pane identities, folders, tab names and focus, verifies legacy settings
  migration/backups, and proves a real shell remains interactive across split
  creation and neighbor closure. Test configuration files stay under artifacts.
- `KeyboardTests.cs` verifies mode negotiation, keypad/navigation identity,
  press/repeat/release, associated text, shifted/base alternates, modifier/lock
  state, focus cleanup and large commits through the real native encoder.
  A window-local Win32 message probe verifies scan-code capture through WPF;
  a real WSL raw-input program verifies exact Kitty bytes through ConPTY.
  Layout and IME fixtures do not establish physical-device or TSF behavior.
- `SettingsTests.cs` invokes the real toolbar button, exercises a compact
  settings window, Apply/Save/Cancel, non-finite input, invalid folders, actual
  write failures, backups, option reload, existing terminal theme updates and
  new-tab defaults. It renders `settings-preview.png`. Clipboard copy-on-select
  and confirmation dialogs still need manual interaction coverage.
- `InteractionTests.cs` uses the real engine for word/line/rectangular gestures,
  scrollback anchors, Select All, resize cancellation, wrapped Unicode URL
  mapping, viewport truncation and explicit-link precedence. It exercises the
  production terminal's hit testing and context-menu Select All. Browser launch,
  clipboard actions and physical edge-autoscrolling still need manual coverage.
- `ChromeTests.cs` renders settings, compact settings, open dropdowns, context
  menus, confirmation dialogs and command-list controls. It checks for broad
  light surfaces, editable-combo text behavior, selection and confirmation
  results. PNGs named `dark-*.png` are available for visual review.
- `WelcomeTests.cs` exercises real PowerShell/CMD/WSL startup, verifies gradient
  colors through ConPTY and executes arithmetic afterward to establish that
  input still reaches an interactive shell. `welcome-preview.png` is rendered
  from a production terminal. No shell profile files are modified.
- `FeatureBatchTests.cs` covers reset/clear semantics, history edges, selection
  and viewport export sources, command identity/availability, effective title,
  search-selection, shared font updates and the real set-size dialog, tab order,
  nested split zoom/equalization/spatial navigation, window state and hover focus.
  Real CMD tests verify session preservation and committed-text blocking while
  read-only is enabled. Clipboard writes, native save-picker interaction and
  physical hover/drag behavior still require manual interaction coverage.
- `SecondBatchTests.cs` exercises absolute/selection/prompt navigation, caret-based
  selection adjustment, escaped styled HTML, native storage limits, null/zero
  settings round trips, invalid reload rejection, preference import/export/defaults,
  nested divider resizing, the row input dialog, advanced settings controls, pointer
  restoration, selection policy, precision wheel deltas and minimized-window cycling.
  A real CMD session remains interactive across import/reload/reset. Advanced dark
  controls are rendered in `advanced-settings.png`. Physical clipboard policies,
  native pickers and launching Notepad are not automated interoperability evidence.
- The ICO is deterministically packaged from upstream's existing PNG artwork at
  16/32/64/128/256 pixels; no replacement logo or affiliation claim is introduced.
  Small sizes use alpha DIBs and the 256-pixel entry uses PNG. `verify-icon.ps1`
  also exercises Windows Shell's real large/small icon extraction.
- The screenshot comes from a controlled demo transcript rendered by the same
  frontend. It is not evidence that the complete upstream GUI was reproduced.
- WSL tests assume the installed default Linux distribution has `/bin/sh`.
- Tests use the current Windows user's token. No claim is made about every
  Windows release, keyboard layout, display configuration or accessibility tool.
- REA's static PE resource parser is used against the built executable to
  preserve artifact identity and inspect the version/manifest resource inventory.
  This does not establish runtime feature parity.

## Remaining release criteria

The detailed, categorized backlog is maintained in [TODO.md](../TODO.md).

Before this can meet the requested "fully featured Windows Ghostty" goal:

1. Extend Kitty graphics beyond the now-tested inline image path: Unicode
   placeholders, animated playback, external transports and broader interoperability.
2. Integrate full shaping, ligatures, font features and renderer performance.
3. Extend the implemented Kitty keyboard event path with IME composition
   geometry, accessibility, third-party interoperability and physical-device
   verification of multi-layout/dead-key/AltGr behavior.
4. Add upstream configuration, keybinding actions, themes and shell integration.
5. Add clipboard protocols, notifications, quick terminal and relevant platform
   desktop integrations, with permission decisions for host-facing effects.
6. Extend the implemented per-window workspace restoration to multi-window
   session collections and optional scrollback persistence. Running process
   state is not restored; current restoration creates fresh shells.
7. Add sustained high-output, cancellation and shutdown stress tests, packaging,
   signing, update strategy and cross-machine validation.

This checklist is outstanding work, not an assertion that those features were
implemented. The output should be distributed as an experimental preview.

Preview 0.9.0 adds ThirdBatchTests for persisted cursor/geometry/style preferences,
invalid-edit atomicity, automatic theme selection, live grid resizing and shared
mouse origin, native line/fraction scroll and line/screen/page selection commands,
and real Settings Apply from the appearance expander. Physical font quality, cursor
blink perception, mixed-DPI transitions and Windows personalization broadcasts
still require desktop coverage. Full self-test now includes animated-startup checks.

FourthBatchTests validates native HTML/ANSI region exports, native palette CSS,
caller-selected whitespace, Unicode CF_HTML byte offsets, tab preference reload,
initial/live header presentation and fresh identities. Real CMD arithmetic verifies
that duplication keeps the original process usable and reopening starts an
independent working ConPTY. Physical formatted-clipboard interoperability,
file-picker/default-application activation and physical middle-click behavior
remain manual verification scope.

FifthBatchTests covers CLI validation/transient launch through real ConPTY,
bounded image decoding/composition and file-handle release, live config reload
with malformed-file recovery/disposal, detached mutable settings drafts, concurrent
clipboard snapshot consistency, queue byte bounds and process-monitor settlement.
Explorer/OSK launching, physical background positioning and OS notifications are
manual scope. The audit is recorded in AUDIT.md.

PartialCompletionTests checks real ABI metadata plus incompatible descriptors;
strict font/theme/profile validation; native cursor blink and word/click options;
explicit paste and contrast policies; real file-path handling through PowerShell,
CMD and a controlled WSL POSIX shell; independent bounded primary selection;
capacity feedback without the status bar; palette metadata/availability; and
managed renderer fault/recovery in one live pane. OOM/native access violations,
physical keyboard/mouse/clipboard interoperability and non-POSIX WSL shells are
not established by these checks.


Preview 0.13 uses native ABI v2 for host identity, scheme reporting and bounded
unknown OSC/APC sampling. Borrowed callbacks are copied under Engine ownership;
reply transport callbacks run outside its lock. Sampling is opt-in and erased on
disable. Pinned config/action matrices document Windows equivalents rather than
claiming upstream config syntax. Inspector counters do not establish native
allocator accounting or GPU profiling.
