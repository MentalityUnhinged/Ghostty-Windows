# Ghostty Windows and VT engine backlog

Audited **31 October 2026**, against Windows Preview **0.6.1** at
`c2043513cde2323115c5f5708c87731d0201c97b`, built on upstream Ghostty
`d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`.

This is the comprehensive known outstanding-feature and release-readiness
backlog for **our Windows application and its Ghostty VT engine**. It includes
missing engine capabilities, engine limitations, engine-to-host integration
gaps and frontend features. It covers the checked-in implementation and pinned
upstream source; it does not claim an exhaustive comparison with future releases.
Every checkbox describes remaining work, including integration and verification
of features whose basic implementation already exists.

Labels: **Missing** = absent from the named layer; **Partial** = extend or correct
existing behavior; **Verify** = implementation exists but coverage is incomplete;
**Decision** = determine Windows applicability or product policy before building.
P0 covers correctness/reliability, P1 core parity, and P2 desktop polish.

Sections 1–16 cover application/frontend work unless stated otherwise. Section
17 records confirmed VT engine gaps; section 18 covers supported engine features
not yet fully exposed by our host; section 19 covers protocol completeness.
An engine gap can require changing `src/terminal/`, not just `windows/`.
Unsupported compatibility extensions are included even when the upstream GUI
also lacks them; they are not all mandatory for matching upstream Ghostty.

## Already implemented — do not rebuild these

Real libghostty-vt/ConPTY sessions; PowerShell/CMD/default-WSL profiles; tabs and
nested resizable splits; per-window workspace restoration into fresh shells;
basic settings with atomic saves/backups; four foreground/background themes;
shared dark application chrome and OS-following title bars; search and plain
scrollback export; cell/word/line/rectangular selection; copy/paste and bracketed
paste; mouse/focus reporting; Kitty keyboard events; HTTP/HTTPS and OSC 8 link
activation; inline Kitty PNG/RGB/RGBA graphics; synchronized image snapshots;
application/shortcut icons; self-contained portable packaging.

The current suite has **177 passing checks**. That count is a baseline, not proof
of complete interoperability or daily-use readiness.

## 1. Correctness and resource ownership — P0

- [ ] **Partial:** Separate the actual upstream engine base from the frontend
  source commit in `windows/build.ps1` provenance. `upstream_commit` currently
  records `git rev-parse HEAD`, which now identifies our frontend commit.
- [ ] **Partial:** Validate the native adapter/managed ABI explicitly at startup,
  including version, struct sizes and supported capabilities.
- [ ] **Partial:** Audit native calls that currently discard result codes;
  distinguish absent values from allocation, capacity and protocol failures.
- [ ] **Partial:** Make renderer, decoder and shell-start failures recoverable
  per pane where possible instead of shutting down the whole application.
- [ ] **Partial:** Handle ConPTY writer saturation and broken pipes explicitly;
  bound queued bytes as well as the existing number of queued messages.
- [ ] **Verify:** Stress close/reopen, shell exit, failed launches, cancellation,
  Job Object cleanup and reader/writer settlement without orphan processes.
- [ ] **Verify:** Exercise resize, screen switches and scrollback eviction during
  active selection, image transfer, synchronized output and search.
- [ ] **Verify:** Exercise rapid focus/tab/split changes with held modifiers,
  composition, mouse capture, open menus and running applications.
- [ ] **Partial:** Audit file-drop quoting for PowerShell, CMD and WSL separately;
  the current shared double-quote treatment is not shell-specific escaping.
- [ ] **Partial:** Make clipboard contention/failure visible and accurately
  report whether a copy succeeded; current copy paths suppress COM failures.

## 2. Upstream configuration and settings — P1

- [ ] **Missing:** Support the upstream Ghostty configuration format, with an
  explicit compatibility/version policy alongside existing Windows JSON settings.
- [ ] **Missing:** Implement config discovery, includes, precedence, repeated
  options, defaults, conditional settings and useful source-located diagnostics.
- [ ] **Missing:** Add open-config, reload-config and file-change reload actions;
  define which options update live and which require a new session.
- [ ] **Missing:** Import upstream theme files/catalogs and complete ANSI palettes,
  cursor, selection and search colors; existing themes change only default FG/BG.
- [ ] **Missing:** Support separate light/dark terminal themes and automatic
  switching without changing the Windows-following native title-bar behavior.
- [ ] **Missing:** Add editable shell profiles: executable, arguments, environment,
  initial command, startup folder and optional wait-after-exit behavior.
- [ ] **Partial:** Expose scrollback/image limits and policies instead of fixed
  adapter constants; preserve explicit unlimited/unspecified semantics where valid.
- [ ] **Missing:** Expose cursor appearance/blink, cell metrics, padding, scroll
  multiplier, selection colors/word boundaries and mouse-behavior preferences.
- [ ] **Missing:** Expose background image/opacity/blur and inactive-pane styling
  once their renderer/platform integrations exist.
- [ ] **Partial:** Add settings reset/defaults, import/export, versioned migration
  and conflict handling when another window or editor changes the same file.
- [ ] **Partial:** Validate unavailable fonts, unsupported themes and profile
  definitions with actionable diagnostics instead of silent fallback alone.
- [ ] **Missing:** Publish a per-option support matrix for `src/config/Config.zig`;
  label implemented, ignored, unsupported and platform-inapplicable settings.

## 3. Keyboard bindings and action coverage — P1

- [ ] **Missing:** User-defined keybindings, remapping and configurable shortcut
  precedence between the application and terminal input.
- [ ] **Missing:** Key sequences/chords, action chains, key tables, one-shot tables,
  unbind, global/all-surface and performable binding semantics.
- [ ] **Missing:** Configurable send-text, CSI, escape and cursor-key actions.
- [ ] **Missing:** Terminal reset, clear-screen, read-only and toggle-mouse-reporting
  actions with documented effects on history and child applications.
- [ ] **Partial:** Font reset/set-size actions and consistent font zoom across
  panes/windows sharing settings; zoom currently refreshes only the active view.
- [ ] **Missing:** Scroll-to-top/bottom/selection/row and configurable fractional
  page/line scrolling actions beyond the current wheel/PageUp/PageDown controls.
- [ ] **Missing:** Keyboard adjustment/extension of selections and search-selection.
- [ ] **Missing:** Copy title, formatted copy and selectable screen/selection export
  actions in addition to existing plain selection copy and scrollback export.
- [ ] **Partial:** Make the command palette cover implemented actions consistently,
  show shortcuts/availability and support richer keyboard navigation.
- [ ] **Missing:** Publish an action support matrix against `src/input/Binding.zig`,
  including deliberate Windows equivalents for platform-specific actions.

## 4. Keyboard devices and IME — P0/P1

- [ ] **Missing:** Correct IME preedit rendering and candidate-window placement at
  the terminal caret using Windows text-input integration.
- [ ] **Partial:** Complete composition cancellation, focus transfers, dead-key
  sequences, reconversion and multi-stage commits without duplicate/lost input.
- [ ] **Verify:** Test real Japanese, Chinese and Korean IMEs; existing commit
  fixtures do not establish TSF/IME behavior.
- [ ] **Verify:** Test physical US/UK, AltGr, AZERTY/QWERTZ, Cyrillic and other layouts,
  including layout changes while keys are held.
- [ ] **Verify:** Test left/right modifiers, Caps/Num/Scroll Lock, keypad navigation,
  unusual keyboards, media/international keys and remote-desktop input.
- [ ] **Verify:** Broaden Kitty keyboard interoperability across real editors,
  shells, multiplexers and nested sessions, including mode push/pop and repeats.
- [ ] **Verify:** Cover legacy modifyOtherKeys and application-keypad/cursor modes
  through real applications, not only native encoder fixtures.
- [ ] **Missing:** User-facing on-screen keyboard action and a documented approach
  to Windows-reserved shortcuts and global hotkeys.

## 5. Typography and rendering — P1

- [ ] **Missing:** Shape font runs across cells, with ligatures and font features,
  while preserving terminal cell geometry and selection/cursor placement.
- [ ] **Missing:** Separate regular/bold/italic/bold-italic families/styles,
  variable-font axes, feature settings and configurable synthetic styles.
- [ ] **Missing:** Codepoint-to-font mappings and richer font discovery/fallback
  diagnostics, including Nerd Fonts and color emoji behavior.
- [ ] **Partial:** Render underline variants/colors, overlines and other decoration
  details currently collapsed or omitted by the adapter's style flags.
- [ ] **Partial:** Implement cursor blink timing, explicit cursor colors/opacity,
  cursor text contrast and configurable focus/unfocus behavior.
- [ ] **Partial:** Audit text blink, wide glyph clipping, combining sequences,
  emoji presentation and adjacent-cell overdraw against upstream behavior.
- [ ] **Missing:** Configurable cell width/height, baseline, line thickness,
  padding/balancing and minimum text contrast.
- [ ] **Missing:** Background images, fit/repeat/position controls and opacity.
- [ ] **Missing:** Window transparency/blur and inactive-split dimming/fill.
- [ ] **Missing:** Custom shader support or an explicit documented alternative.
- [ ] **Partial:** Optimize render damage, allocations, glyph/image caches and
  URL/search recomputation; benchmark before choosing a new renderer architecture.
- [ ] **Verify:** Mixed-DPI monitor moves, scaling/font changes, HDR/color handling,
  emoji/box-drawing consistency and long-running memory use.

## 6. Images and graphics protocols — P1

- [ ] **Missing:** Kitty Unicode-placeholder placement support.
- [ ] **Missing:** Animated-image frames, playback timing and animation controls.
- [ ] **Missing:** Complete Kitty file/temp-file/shared-memory transports with a
  Windows-specific resource lifetime and access policy.
- [ ] **Partial:** Audit relative/virtual placements, placement identifiers,
  deletion variants, image queries/replies and transmission corner cases.
- [ ] **Partial:** Expose image storage limits and define managed decoded-image
  cache budgets alongside the existing native storage limit.
- [ ] **Verify:** Exercise malformed/chunked/compressed payloads, allocation
  failure, eviction, reflow, resize, scrollback and alternate-screen transitions.
- [ ] **Verify:** Run interoperability tests with real image tools and multiplexers;
  the inline-image smoke tests do not establish full Kitty graphics parity.
- [ ] **Decision:** Document which other image protocols are upstream-supported
  and relevant to this port before advertising or implementing them.

## 7. Shell integration and semantic terminal state — P1

- [ ] **Missing:** Optional installation/removal of shell integration for
  PowerShell, CMD where feasible, and supported shells inside WSL.
- [ ] **Missing:** Prompt/command boundaries, command execution state, exit status
  and command duration surfaced in the Windows UI.
- [ ] **Missing:** Previous/next prompt navigation and select/copy command output
  using the engine's semantic markers.
- [ ] **Missing:** Click-to-move-cursor at supported shell prompts.
- [ ] **Missing:** Shell-driven cursor behavior, title policy and integration
  feature switches with graceful behavior in unsupported shells.
- [ ] **Partial:** Map WSL working-directory reports using explicit distribution
  identity; current new-session folder inheritance accepts local Windows paths only.
- [ ] **Missing:** Shell-aware close confirmation distinguishing idle prompts from
  active commands; current confirmation counts terminal sessions.
- [ ] **Missing:** Long-running-command completion notifications and activity/busy
  indicators driven by verified shell integration state.

## 8. Clipboard, links, selection and mouse — P1/P2

- [ ] **Missing:** OSC 52 clipboard read/write integration with explicit policy,
  limits, confirmation/grants and failure responses.
- [ ] **Decision:** Determine scope for newer clipboard protocols exposed by the
  pinned engine; document unsupported operations instead of silently claiming parity.
- [ ] **Missing:** Configurable clipboard transforms, trailing-space policy,
  formatted copy and separate copy-selection/title/URL actions.
- [ ] **Partial:** Improve paste protection around bracketed-paste safety, escape
  filtering and multiline policy; avoid losing text without clear behavior.
- [ ] **Missing:** Configurable right/middle-click actions and primary-selection
  equivalents appropriate to Windows.
- [ ] **Missing:** Configurable word boundaries, click-repeat behavior,
  clear-on-copy/typing and semantic-output selection gestures.
- [ ] **Missing:** Selection extension by keyboard and convenient scroll-to-selection.
- [ ] **Partial:** Replace heuristic-only URL handling with configurable matchers,
  and define file/line/email link handling and launch policy before enabling it.
- [ ] **Partial:** Consider full logical-line link detection outside the viewport;
  current detection deliberately skips truncated URLs.
- [ ] **Partial:** Add stronger link affordances/previews and keep hover state
  accurate when modifiers, output or viewport change without mouse movement.
- [ ] **Missing:** Hide pointer while typing and configurable mouse reporting,
  Shift-override behavior and scroll speed/precision.
- [ ] **Verify:** Physical selection/autoscroll, capture loss, browser activation,
  copy-on-select and clipboard contention across real applications and displays.
- [ ] **Verify:** Expand touchpad/high-resolution wheel, touch, pen and horizontal
  scrolling coverage; define behavior where no implementation exists.

## 9. Search and export — P1/P2

- [ ] **Partial:** Unify search-result navigation and rendered highlighting so
  matching rules agree across Unicode and soft-wrapped lines.
- [ ] **Missing:** Previous-result controls, search-selection, case/whole-word
  options and optional regex search with useful error reporting.
- [ ] **Partial:** Distinguish the active result from other matches and expose
  current-result position/count and wraparound state accessibly.
- [ ] **Partial:** Move expensive search work off the UI path with cancellation;
  current engine search runs synchronously under the terminal lock.
- [ ] **Missing:** Export visible screen or selection separately, including optional
  styled/HTML output and configurable open-in-editor behavior.
- [ ] **Partial:** Add cancellation, capacity handling and actionable IO errors for
  large search/export operations.

## 10. Tabs, splits, windows and sessions — P1/P2

- [ ] **Missing:** Reorder tabs by dragging/keyboard, detach tabs into new windows
  and move tabs/panes between windows without replacing live sessions.
- [ ] **Missing:** Split zoom/unzoom, equalize splits and keyboard resizing.
- [ ] **Partial:** Spatial split navigation; current arrow shortcuts cycle the
  pane list rather than selecting the visually adjacent pane.
- [ ] **Missing:** Tab overview, configurable tab placement/visibility and richer
  title customization for panes, tabs and windows.
- [ ] **Missing:** Undo/reopen closed surfaces with a defined lifetime policy;
  destroyed ConPTY processes cannot simply be resumed.
- [ ] **Partial:** Preserve/restore pre-fullscreen window style, bounds and state
  rather than always returning to a normal window.
- [ ] **Missing:** Always-on-top, decorations visibility, reset-window-size,
  focus-follows-mouse and window-switching actions.
- [ ] **Partial:** Persist monitor identity/position and restore safely when monitor
  topology, DPI or work areas change.
- [ ] **Missing:** Multi-window workspace collections and named workspace management;
  current saves restore only the most recently saved window.
- [ ] **Decision:** Optional scrollback persistence/crash recovery, including storage
  format, privacy, retention and corrupt-data handling; live process restoration
  is not implied by layout restoration.
- [ ] **Missing:** Single-instance routing and CLI requests to open a tab/window
  or launch a command in a selected existing instance.
- [ ] **Partial:** Make session limits configurable where appropriate and expose
  capacity/error feedback consistently even when the status bar is hidden.

## 11. Windows desktop integration — P2

- [ ] **Missing:** Quick/drop-down terminal with global shortcut, placement,
  monitor selection, size, animation and focus-loss policy.
- [ ] **Missing:** Toast notifications and bell handling, with mute/attention rules
  and application-controlled notification protocols bridged to Windows.
- [ ] **Missing:** Taskbar progress/activity badges, jump lists and session actions.
- [ ] **Missing:** Optional tray/background mode and configurable quit-after-last-window.
- [ ] **Missing:** Explorer "open terminal here" integration and configurable
  default-terminal support where Windows exposes the required contract.
- [ ] **Missing:** Explicit PowerShell 7 and named WSL distribution/profile discovery,
  rather than only the three hardcoded launch profiles.
- [ ] **Missing:** Windows-friendly command-line flags for command, directory,
  profile, title and diagnostics, with documented quoting and exit behavior.
- [ ] **Decision:** URL/protocol registration and associated-file handling; enable
  only deliberately supported entry points.
- [ ] **Partial:** Complete window-theme/high-contrast behavior for app-owned chrome;
  shared dark styles currently remain dark while native title bars follow Windows.
- [ ] **Verify:** Native file/folder pickers, taskbar icons, notifications and focus
  behavior across supported Windows versions; pickers remain OS-owned surfaces.

## 12. Accessibility and localization — P1

- [ ] **Missing:** A UI Automation text provider for terminal contents, caret,
  selection, scrollback and changes, with screen-reader-friendly event handling.
- [ ] **Partial:** Complete accessible names, descriptions, focus order and keyboard
  access for every toolbar, menu, tab, split, search and settings control.
- [ ] **Partial:** Audit custom dark templates for high contrast, focus visibility,
  disabled/checked/submenu states, scaling and keyboard-only interaction.
- [ ] **Missing:** Localization resources and language selection for Windows UI text,
  including long translations and right-to-left dialog layouts.
- [ ] **Verify:** Narrator and other screen readers, magnification, large text,
  reduced motion and real high-contrast configurations.

## 13. Diagnostics and developer tools — P1/P2

- [ ] **Missing:** A useful terminal inspector for modes, cursor, rendering,
  input/protocol events and per-pane performance/resource state.
- [ ] **Partial:** Structured, bounded diagnostics and crash reports with artifact
  identity and a deliberate policy for terminal contents and sensitive data.
- [ ] **Missing:** User-facing diagnostics export and clear reporting of unsupported
  configuration, protocol integration and native capability mismatches.
- [ ] **Partial:** Document support boundaries and an actionable failure path for
  shell launch, settings, renderer, clipboard and export failures.

## 14. Performance and compatibility validation — P0/P1

- [ ] **Verify:** Sustained high-output throughput, UI latency, CPU/GPU usage and
  bounded memory while rendering text, Unicode, images and selections.
- [ ] **Verify:** Long-lived sessions, repeated tabs/splits/windows, rapid resize,
  resource exhaustion, settings failures and shutdown under load.
- [ ] **Verify:** Windows 10/11 cross-machine testing, including different GPU,
  DPI, keyboard, filesystem, permissions and shell configurations.
- [ ] **Verify:** Real PowerShell 5/7, CMD and multiple WSL distributions/shells.
- [ ] **Verify:** Editors, pagers, fuzzy finders, REPLs and multiplexers, including
  alternate screen, mouse, keyboard negotiation and remote/nested sessions.
- [ ] **Verify:** Relevant VT conformance suites and host callback integrations;
  distinguish upstream engine conformance from Windows frontend coverage.
- [ ] **Verify:** Cancellation, malformed input, capacity, target/session identity,
  failed cleanup and permission failures at public application boundaries.
- [ ] **Partial:** Build a repeatable manual interaction matrix for browser launch,
  clipboard, IME, accessibility and physical pointer/device behavior.

## 15. Build, packaging and release — P0/P1

- [ ] **Missing:** Windows-specific CI that builds and runs real frontend tests;
  inherited upstream workflows are currently disabled in the private repository.
- [ ] **Partial:** Document/validate a clean-machine pinned toolchain and dependency
  acquisition; current scripts default to this user's local tool directories.
- [ ] **Partial:** Publish reproducible source/runtime provenance and checksums
  covering the correct source revision, engine base and all shipped dependencies.
- [ ] **Missing:** Installer/uninstaller and optional MSIX/package-manager delivery
  while retaining a supported portable distribution.
- [ ] **Missing:** Code signing, release verification and a defined update/check/
  rollback strategy; auto-update is not implemented.
- [ ] **Decision:** ARM64 or other architectures, minimum Windows versions and
  whether they are release targets or explicitly unsupported.
- [ ] **Partial:** Audit notices and licensing for bundled runtime, engine,
  artwork, themes/fonts and any future dependencies.
- [ ] **Missing:** Release channels, compatibility policy, changelog, migration
  notes, installation/troubleshooting docs and versioned support claims.
- [ ] **Partial:** Publish versioned build artifacts/releases separately from Git;
  generated ZIPs, executables, local evidence and test outputs remain ignored.
- [ ] **Partial:** Plan regular upstream merges/rebases and rerun the relevant
  interoperability and regression checks after engine/header changes.
- [ ] **Decision:** When ready for public distribution, create an official public
  Ghostty fork and transfer the Windows branch with its history. GitHub cannot
  register this private copy as a private fork of the public upstream.

## 16. Scope decisions and release exit criteria

- [ ] **Decision:** Classify upstream macOS/GTK/X11/Wayland-only options/actions as
  Windows equivalents or intentionally inapplicable, rather than copying them blindly.
- [ ] **Decision:** Set an explicit target for "feature complete": feature matrix,
  supported configurations, interoperability evidence and accepted exclusions.
- [ ] **Partial:** Keep this checklist current as items land; record verification
  evidence before turning a **Verify** item into a completed claim.
- [ ] **Partial:** Resolve P0 correctness issues and document any accepted remaining
  limitations before describing the preview as suitable for daily use.

## 17. VT engine capabilities and limitations — P1/P2

These items come from the pinned source, not from assuming that every
unsupported Windows feature is missing in libghostty. They remain outstanding
even when matching the current upstream GUI would not require implementing them.

- [ ] **Missing — engine:** Add Sixel DCS parsing, image state and rendering
  integration if targeting Sixel applications. The pinned
  [DCS dispatcher](src/terminal/dcs.zig) handles tmux control, XTGETTCAP and
  DECRQSS, but has no Sixel hook. Do not advertise Sixel until the complete
  decode/storage/placement/render path and limits are implemented and tested.
- [ ] **Missing — engine:** Support extended charset designation intermediates
  `-`, `.` and `/`; `configureCharset` in
  [stream.zig](src/terminal/stream.zig) explicitly lists them as TODOs while
  supporting `(`, `)`, `*` and `+`. Include the appropriate charset semantics,
  not merely accepting additional parser bytes.
- [ ] **Missing — engine/platform:** Implement a Windows-compatible Kitty shared
  memory transport, or document a deliberate compatibility exclusion.
  [graphics_image.zig](src/terminal/kitty/graphics_image.zig) explicitly returns
  `UnsupportedMedium` on Windows. Setting the existing medium-enabled option
  in the host cannot supply the missing implementation. See section 6 for the
  rest of image transport integration and verification.
- [ ] **Missing — engine/host:** Implement Kitty drag-out negotiation, data
  transfer and platform drag initiation if supporting the complete drag-and-drop
  protocol. [dnd_drop.zig](src/terminal/kitty/dnd_drop.zig) accepts/ignores source
  enable/disable offers and refuses new drag-out offers/start/data requests.
- [ ] **Missing — engine/host:** Implement OSC 1337 inline/multipart file and
  image transfer commands `File`, `MultipartFile`, `FilePart` and `FileEnd`.
  These appear in the unimplemented-command branch of the
  [iTerm2 parser](src/terminal/osc/parsers/iterm2.zig); Kitty graphics support
  does not implement this separate protocol.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `RequestUpload`, `CopyToClipboard` and `EndCopy`. They are unimplemented in
  that parser; its existing `Copy` command is a distinct, supported operation.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `AddAnnotation`, `AddHiddenAnnotation`, `SetMark`, `Block`, `Button`, `Custom`,
  `ClearCapturedOutput` and `ClearScrollback`.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `CursorShape`, `HighlightCursorLine`, `SetColors`, `SetBackgroundImageFile`,
  `SetBadgeFormat` and `SetProfile`.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `SetKeyLabel`, `PushKeyLabels` and `PopKeyLabels`.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `RemoteHost`, `ReportCellSize`, `ReportVariable`, `SetUserVar`,
  `ShellIntegrationVersion` and `UnicodeVersion`. The parser's implemented
  `CurrentDir` operation does not establish support for these other commands.
- [ ] **Missing — engine/host:** Implement or explicitly exclude OSC 1337
  `OpenURL`, `RequestAttention`, `StealFocus` and `Disinter`. Define host-facing
  policy before connecting requests to browser, focus or desktop actions.
- [ ] **Decision — engine:** Decide whether to implement changeable DECECM
  (DEC private mode 117). [modes.zig](src/terminal/modes.zig) deliberately reports
  it as permanently reset. This is a documented fixed behavior, not an unknown
  mode or an accidental failure to parse its query.
- [ ] **Decision — engine:** Decide whether to add the currently fixed-reset
  ANSI modes GATM, SRTM, VEM, HEM, PUM, FEAM, FETM, MATM, TTM, SATM, TSM and
  EBM (1, 5, 7, 10, 11, 13–19). Their unsupported behavior is explicitly
  reported by `ModeState.getReport`; do not claim support merely because
  DECRQM can report them.
- [ ] **Decision — engine:** Decide the supported glyph-protocol font subset.
  [glyph request handling](src/terminal/apc/glyph/request.zig) can report
  `CompositeUnsupported` and `HintingUnsupported`; implement the required font
  processing if broadening that subset, with resource limits and regression
  coverage. Distinguish these deliberate rejection paths from host rendering gaps.
- [ ] **Partial — engine:** Finish the selection-boundary TODO in
  [SelectionGesture.zig](src/terminal/SelectionGesture.zig): clamp selection to
  the screen area/last written row where appropriate. Test forward, reverse,
  rectangular and empty-area drags rather than masking it in the WPF renderer.
- [ ] **Partial — engine:** Implement deferred reflow for non-viewport/non-active
  pages if required for large-history resize performance.
  [PageList.resize](src/terminal/PageList.zig) explicitly notes that resize
  currently decompresses all nodes and leaves deferred reflow as a TODO.
- [ ] **Verify — engine/platform:** Validate Windows scrollback compression and
  memory reclamation capabilities. `PageList` can report compression as
  unsupported when memory reclamation is unavailable; distinguish supported,
  disabled and unsupported states before exposing a compression preference.
- [ ] **Verify — engine:** Review remaining source TODOs and untested boundary
  paths in screen/page/selection/reflow/formatting code. Record specific
  externally observable gaps and regression cases; do not convert every
  maintenance TODO into a supposed missing terminal feature.

## 18. VT engine-to-Windows integration gaps — P1

The engine already exposes these capability families in
[terminal.h](include/ghostty/vt/terminal.h). Remaining work is to configure,
consume or validate them in our adapter/renderer, not to invent another parser.
Existing related backlog items remain in their owning sections.

- [ ] **Missing — integration:** Supply host-specific enquiry and XTVERSION
  responses and review device-attribute/terminfo identity against the actual
  Windows implementation. Audit defaults before overriding them.
- [ ] **Missing — integration:** Connect host size-query and window-resize
  requests to measured cell/pixel/window geometry and a defined resize policy.
- [ ] **Missing — integration:** Notify the engine of the host color scheme and
  validate application theme queries/reports when OS or terminal themes change.
- [ ] **Missing — integration:** Bridge the engine's glyph protocol into usable
  Windows font/glyph rendering; enabling a protocol flag alone is insufficient.
- [ ] **Partial — integration:** Audit OSC text-sizing and other rendering metadata
  against the C render API and WPF output; the current cell snapshot does not
  establish complete support for every engine-side text representation.
- [ ] **Missing — integration:** Connect bounded unknown-sequence reporting to
  diagnostics so unsupported protocol behavior can be investigated without
  treating arbitrary terminal payloads as commands.
- [ ] **Missing — integration:** Surface engine program-status and hierarchical/
  semantic state where applicable, with typed ownership and explicit unknowns.
- [ ] **Verify — integration:** Cover terminal continuation/checkpoint/snapshot
  APIs if used for recovery or persistence. Existing engine APIs do not imply
  that our workspace restoration already restores terminal state.

Other host integrations already tracked above: clipboard read/write and Kitty
clipboard responses (section 8); bell, desktop notifications and progress
(section 11); semantic prompts and command state (section 7); cursor/decorations/
palette metadata (section 5); image placement/transport (section 6). These are
engine-supported areas with incomplete Windows integration, not blanket claims
that the engine is missing all those protocols.

## 19. VT protocol completeness and capability accounting — P0/P1

- [ ] **Missing — audit:** Create a sequence-level support matrix for C0/C1, ESC,
  CSI, OSC, DCS and APC, including parser recognition, state mutation, replies,
  public C API exposure and host rendering/side effects.
- [ ] **Partial — audit:** Inventory valid but unimplemented dispatch forms in
  `stream.zig`, `osc.zig` and `dcs.zig`, with concrete sequence/parameter examples.
  Separate standards-compliant missing forms from malformed, reserved or
  intentionally ignored input; generic "unimplemented" logs are not enough.
- [ ] **Verify — engine:** Exercise mode set/reset/save/restore/query, device
  attributes, status reports, terminal capability queries, window operations,
  character sets and rectangular operations through real terminal byte streams.
- [ ] **Verify — engine/host:** Cross-check every advertised capability against
  the compiled build and host implementation, including compile-time options,
  optional callbacks and operating-system restrictions.
- [ ] **Decision — compatibility:** Define the intended VT/xterm/Kitty/iTerm2
  compatibility targets and legacy extensions to add beyond current upstream.
  Unsupported features remain visible in the matrix even when intentionally
  excluded from a release.
- [ ] **Verify — engine/host:** Add differential and conformance evidence with
  reference terminals and representative applications. A feature must have
  correct state, rendering and replies; acceptance by the parser alone is not
  evidence of end-to-end implementation.

## Audit references

- [Current frontend capabilities and limitations](windows/README.md)
- [Architecture, verification boundaries and release criteria](windows/PORTING.md)
- [Settings model](windows/Settings.cs), [window/actions](windows/MainWindow.cs),
  [rendering/input](windows/TerminalView.cs), [ConPTY ownership](windows/PseudoConsole.cs)
- [Native adapter and host integrations](windows/native/bridge.c)
- [Upstream configuration surface](src/config/Config.zig)
- [Upstream keybinding/action surface](src/input/Binding.zig)
- [VT dispatch](src/terminal/stream.zig), [DCS handling](src/terminal/dcs.zig),
  [mode support](src/terminal/modes.zig), [OSC 1337](src/terminal/osc/parsers/iterm2.zig)
- [VT C embedding contract](include/ghostty/vt/terminal.h)

Optional product extensions and intentionally unsupported upstream platform
features require an explicit decision; their presence here is not a claim that
the current engine or every upstream platform implements them.
