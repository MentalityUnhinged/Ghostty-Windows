# Ghostty Windows and VT engine backlog

Reconciled **1 November 2026** against the **local Windows Preview 0.13.0**
source and its tested binaries. Original audit baseline: Preview 0.6.1 at
`c2043513cde2323115c5f5708c87731d0201c97b` on 31 October. Engine base remains
`d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`.

This is the comprehensive known outstanding-feature and release-readiness
backlog for **our Windows application and its Ghostty VT engine**. It includes
missing engine capabilities, engine limitations, engine-to-host integration
gaps and frontend features. It covers the reconciled Windows implementation and pinned
upstream source; it does not claim an exhaustive comparison with future releases.
Unchecked boxes describe remaining work, including integration and verification
of features whose basic implementation already exists. Checked entries record
features delivered in the identified preview.

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

## Current picture

| Status | Open groups | Meaning |
| --- | ---: | --- |
| Missing | 47 | Absent capabilities or engineering work; some are optional/legacy compatibility |
| Partial | 43 | Working features with specific extensions, refinements or release work left |
| Verify | 25 | Coverage still incomplete; this does not mean the feature is absent |
| Decision | 11 | Product/platform policy to settle before implementation |
| **Total** | **126** | **Grouped checklist entries, not 127 missing terminal features** |

The published GitHub baseline had **173 open groups** (84 Missing, 49 Partial,
28 Verify, 12 Decision). This reconciliation leaves **126**. That net reduction
includes one newly confirmed rectangle-operation gap. Many delivered features
were subrequirements of a broader group, so a 20-feature batch did not necessarily
close 20 whole backlog entries. The historical delivery tables are at the end.

**Today's work already delivered:** custom keybindings/precedence/unbind/send
commands; inspector and local diagnostic exports; host identity/theme reports;
bounded unknown-protocol capture; complete pinned config/action inventories;
plus the prior batches of appearance, clipboard, search/export, tab/split/window
and settings features. The ConPTY read/dispose shutdown race is fixed.

**Evidence:** 442 full regression checks, 71 extended checks and a production
compression probe passed. Three of the original 28 verification tasks are closed;
25 retain explicit remaining coverage in [VERIFICATION.md](windows/VERIFICATION.md).
Those include real hardware/IMEs/accessibility, other OS/WSL environments,
long-duration stress, fault injection and independent conformance/reference tests.
Automated check counts are not a claim that those requirements are satisfied.

**Most useful next work:**

1. Core usability: IME preedit/candidate placement, shell integration/state,
   accessible terminal text and search correctness/navigation.
2. Configuration parity: upstream config/theme loading, editable profiles,
   complete palettes and advanced keybinding semantics.
3. Rendering/protocol parity: shaping/ligatures/font fallback, remaining image
   features/transports, OSC52, sizing/glyph callbacks and explicit VT gaps.
4. Desktop/session polish: tab dragging/detachment, named multi-window workspaces,
   drop-down mode, notifications/taskbar/tray and Explorer integration.
5. Release readiness: Windows CI, clean-machine builds, installer/signing/update
   policy, reproducible releases/licensing and the remaining verification matrix.

**Repository/build distinction:** this list describes the implemented Windows
source and tested local build. A Git commit/push publishes source; it does not
publish the portable EXE/ZIP as a GitHub Release. Generated builds and local
traces stay outside Git. Keep this summary in sync when changing the list.

## Already implemented — do not rebuild these

Real libghostty-vt/ConPTY sessions; PowerShell/CMD/default-WSL profiles; tabs and
nested resizable splits; per-window workspace restoration into fresh shells;
basic settings with atomic saves/backups; four foreground/background themes;
shared dark application chrome and OS-following title bars; search and plain
scrollback export; cell/word/line/rectangular selection; copy/paste and bracketed
paste; mouse/focus reporting; Kitty keyboard events; HTTP/HTTPS and OSC 8 link
activation; inline Kitty PNG/RGB/RGBA graphics; synchronized image snapshots;
application/shortcut icons; self-contained portable packaging.

The audit baseline had **177 passing checks**. That count is a baseline, not proof
of complete interoperability or daily-use readiness.

## Remaining work by area

| Area | Missing | Partial | Verify | Decision |
| --- | ---: | ---: | ---: | ---: |
| 1. Correctness and resource ownership — P0 | 0 | 2 | 3 | 0 |
| 2. Upstream configuration and settings — P1 | 3 | 3 | 0 | 0 |
| 3. Keyboard bindings and action coverage — P1 | 0 | 1 | 0 | 0 |
| 4. Keyboard devices and IME — P0/P1 | 1 | 1 | 5 | 0 |
| 5. Typography and rendering — P1 | 3 | 6 | 1 | 0 |
| 6. Images and graphics protocols — P1 | 3 | 2 | 2 | 1 |
| 7. Shell integration and semantic terminal state — P1 | 6 | 2 | 0 | 0 |
| 8. Clipboard, links, selection and mouse — P1/P2 | 1 | 2 | 2 | 1 |
| 9. Search and export — P1/P2 | 0 | 5 | 0 | 0 |
| 10. Tabs, splits, windows and sessions — P1/P2 | 2 | 4 | 0 | 1 |
| 11. Windows desktop integration — P2 | 6 | 1 | 1 | 1 |
| 12. Accessibility and localization — P1 | 2 | 2 | 1 | 0 |
| 13. Diagnostics and developer tools — P1/P2 | 0 | 1 | 0 | 0 |
| 14. Performance and compatibility validation — P0/P1 | 0 | 1 | 7 | 0 |
| 15. Build, packaging and release — P0/P1 | 4 | 5 | 0 | 1 |
| 16. Scope decisions and release exit criteria | 0 | 1 | 0 | 2 |
| 17. VT engine capabilities and limitations — P1/P2 | 12 | 2 | 0 | 3 |
| 18. VT engine-to-Windows integration gaps — P1 | 3 | 1 | 0 | 0 |
| 19. VT protocol completeness and capability accounting — P0/P1 | 1 | 1 | 3 | 1 |

Counts below describe only unchecked backlog entries. Completed entries remain
visible beside their remaining extensions to preserve context.

## 1. Correctness and resource ownership — P0

- [x] **Implemented 0.11.0:** Separate the pinned engine base and frontend revision,
  dirty-source state, engine-tree match and archive rebuild status in provenance.
- [x] **Implemented 0.12.0:** Validate the native adapter ABI before callbacks or terminal allocation: version, cell/image/system-image sizes, pointer width and required capabilities. Mismatches give matching-build recovery instructions.
- [ ] **Partial:** Audit native calls that currently discard result codes;
  distinguish absent values from allocation, capacity and protocol failures.
- [x] **Implemented 0.12.0:** Managed font/renderer/output failures are isolated to their pane with recovery feedback. Drawing contexts are isolated and last-good cell metrics retained; malformed images and failed shell starts remain recoverable. Native access violations and process-wide allocation exhaustion are not masked.
- [ ] **Partial:** Complete native cleanup-failure handling and fault injection. Input byte/message bounds, reader/writer failure reporting, single-start ownership and monitor settlement are implemented. The 1 November read/dispose race is fixed: pumps settle before stream disposal; ten gated natural-exit cases pass. Remaining descendant/Linux-side/cancellation coverage is V01/V22.
- [ ] **Verify:** Stress close/reopen, shell exit, failed launches, cancellation,
  Job Object cleanup and reader/writer settlement without orphan processes.
- [ ] **Verify:** Exercise resize, screen switches and scrollback eviction during
  active selection, image transfer, synchronized output and search.
- [ ] **Verify:** Exercise rapid focus/tab/split changes with held modifiers,
  composition, mouse capture, open menus and running applications.
- [x] **Implemented 0.12.0:** File-drop insertion is shell-specific: PowerShell literal arguments, CMD quoted arguments with expansion-sensitive names refused, and WSL POSIX arguments after live wslpath translation. Unknown custom-command shells are refused rather than guessed.
- [x] **Implemented 0.12.0:** Copy failure is reported and selection cleared only after success. Clipboard-read contention is visible; unknown clipboard availability permits an explicit retry instead of being treated as empty.

## 2. Upstream configuration and settings — P1

- [ ] **Missing:** Support the upstream Ghostty configuration format, with an
  explicit compatibility/version policy alongside existing Windows JSON settings.
- [ ] **Missing:** Implement config discovery, includes, precedence, repeated
  options, defaults, conditional settings and useful source-located diagnostics.
- [x] **Implemented 0.11.0:** Opt-in automatic JSON reload, debounce and nonfatal recovery. Upstream text-config parsing/discovery is tracked separately above; it does not make JSON reload unfinished.
- [ ] **Partial:** Import upstream theme files/catalogs and apply complete per-theme ANSI palettes. Cursor, cursor-text, selection and search colors are already configurable; automatic light/dark terminal themes also work. Theme-file/palette parity remains.
- [x] **Implemented 0.9.0:** Support separate light/dark terminal themes and automatic
  switching without changing the Windows-following native title-bar behavior.
- [ ] **Missing:** Add editable shell profiles: executable, arguments, environment,
  initial command, startup folder and optional wait-after-exit behavior.
- [ ] **Partial:** Expose scrollback line limits and additional image/cache
  policies. Byte limits with explicit default/unlimited/disabled semantics
  landed in 0.8.0.
- [x] **Implemented 0.12.0:** Expose cursor appearance/blink, cell metrics/padding, scroll command amounts, selection/search colors, custom Unicode word boundaries, repeat-click preferences and local mouse policies.
- [x] **Implemented 0.9.0-0.12.0:** Acrylic/background image appearance controls and inactive content dimming are delivered. Remaining transparency/blur/fill and animated-background work is tracked once in section 5.
- [ ] **Partial:** Add versioned preference migration and concurrent edit conflict
  handling. Reset/defaults and import/export landed in 0.8.0.
- [x] **Implemented 0.12.0:** Regular/style font families, named themes, font sizes and built-in default profiles are validated with actionable errors instead of silent substitution. Editable custom profiles remain a separate missing feature.
- [x] **Implemented 0.13.0:** Complete 219-field pinned [configuration inventory](windows/CONFIG_SUPPORT.md), with source identity and explicit Windows-equivalent/unsupported status. No unsupported syntax is silently treated as implemented or platform-inapplicable.

## 3. Keyboard bindings and action coverage — P1

- [x] **Implemented 0.13.0:** JSON single-chord bindings, remapping via unbind, validated modifiers/conflicts, configurable application/terminal precedence, effective palette labels and reserved recovery shortcuts.
- [ ] **Partial:** Single chords and unbind are implemented. Key sequences, action chains, key tables, one-shot tables and global/all-surface/performable semantics remain.
- [x] **Implemented 0.13.0:** UTF-8 send-text, CSI/escape prefixes and native cursor-key encoding actions, respecting read-only/input policy.
- [x] **Implemented 0.7.0:** Terminal reset, clear-screen, read-only and toggle-mouse-reporting
  actions with documented effects on history and child applications.
- [x] **Implemented 0.7.0:** Font reset/set-size actions and consistent font zoom
  across panes/windows sharing settings.
- [x] **Implemented 0.9.0:** Configurable fractional page/line command actions;
  scroll-to-selection/row landed in 0.8.0 and top/bottom in 0.7.0.
- [x] **Implemented 0.9.0:** Home/End/Page selection actions beyond the arrow-key
  selection extension delivered in 0.8.0; search-selection landed in 0.7.0.
- [x] **Implemented 0.10.0:** Formatted HTML clipboard and HTML/ANSI region exports.
  HTML scrollback export landed in 0.8.0; title copy and plain-text viewport/
  selection export landed in 0.7.0.
- [x] **Implemented 0.12.0:** Palette covers implemented command actions, including tab/selection/search/preview and clipboard actions. One chord owner supplies matching and labels, and availability/state notifications refresh while the palette is open.
- [x] **Implemented 0.13.0:** Complete pinned [88-action inventory](windows/ACTION_SUPPORT.md), with explicit Windows equivalents and unsupported payload/platform behavior.

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
- [x] **Implemented 0.12.0:** Windows on-screen keyboard action is available; Windows-reserved and global-shortcut boundaries are documented in KEYBINDS.md. No global hook or reserved-key override is installed.

## 5. Typography and rendering — P1

- [ ] **Missing:** Shape font runs across cells, with ligatures and font features,
  while preserving terminal cell geometry and selection/cursor placement.
- [ ] **Partial:** Separate bold/italic families landed in 0.9.0, bold-italic in 0.10.0;
  variable-font axes, feature settings and configurable synthetic styles.
- [ ] **Missing:** Codepoint-to-font mappings and richer font discovery/fallback
  diagnostics, including Nerd Fonts and color emoji behavior.
- [ ] **Partial:** Render underline variants/colors, overlines and other decoration
  details currently collapsed or omitted by the adapter's style flags.
- [x] **Implemented 0.12.0:** Cursor colors/opacity, focus appearance, explicit/automatic block-cursor text contrast and opt-in application-controlled blink are implemented. The adapter preserves the native blinking/steady flag and application cursor visibility.
- [ ] **Partial:** Audit text blink, wide glyph clipping, combining sequences,
  emoji presentation and adjacent-cell overdraw against upstream behavior.
- [x] **Implemented 0.12.0:** Cell multipliers, baseline offset, decoration thickness, balanced padding and palette-based minimum text contrast are configurable. Contrast is best-achievable against cell colors, not a promise about a translucent desktop backdrop.
- [ ] **Partial:** Background images, fit/repeat/alignment and opacity are implemented, including
  vertical positioning in 0.12.0. Animated backgrounds remain.
- [ ] **Partial:** Windows 11 Acrylic blur with adjustable background tint opacity and solid fallback is implemented. Clear unblurred transparency, independent blur strength and inactive-split fill remain. Content dimming landed in 0.9.0.
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
- [ ] **Partial:** Add explicit managed decoded-image cache budgets and cache-pressure policy. Native image storage limits are already configurable with default/unlimited/disabled semantics.
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
- [ ] **Partial:** Add select/copy semantic command output. Previous/next prompt
  navigation using existing OSC 133 markers landed in 0.8.0.
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
- [x] **Implemented 0.10.0-0.12.0:** Clipboard whitespace/newline policies, formatted selection copy, screen/history copy and last-hovered URL copy are delivered. Configurable link matching remains a separate item below.
- [x] **Implemented 0.12.0:** Paste controls have explicit Reject/Strip policies, visible removal counts and no silent ESC removal. Multiline confirmation can remain mandatory in bracketed mode; single-line paste preserves text while normalizing line breaks.
- [x] **Implemented 0.12.0:** Opt-in Ghostty-local primary selection is separate from the Windows clipboard, shared across panes/windows in this process, bounded to 8 MiB and never persisted. Middle-click and palette primary paste use the same paste policy.
- [x] **Implemented 0.8.0-0.12.0:** Word boundaries, repeat-click preferences and clear-on-copy/typing are delivered. Remaining semantic command-output selection is tracked once in section 7.
- [x] **Implemented 0.8.0:** Arrow-key selection extension and scroll-to-selection.
- [ ] **Partial:** Replace heuristic-only URL handling with configurable matchers,
  and define file/line/email link handling and launch policy before enabling it.
- [ ] **Partial:** Consider full logical-line link detection outside the viewport;
  current detection deliberately skips truncated URLs.
- [x] **Implemented 0.12.0:** HTTP(S) links have tooltip/hand previews that refresh on output, viewport, layout, pointer re-entry and modifier changes. Last-hovered address copy is available without launching a browser.
- [x] **Implemented 0.12.0:** Shift-for-local-mouse override is configurable; hide-pointer-on-typing, local mouse reporting and wheel speed/precision remain available.
- [ ] **Verify:** Physical selection/autoscroll, capture loss, browser activation,
  copy-on-select and clipboard contention across real applications and displays.
- [ ] **Verify:** Expand touchpad/high-resolution wheel, touch, pen and horizontal
  scrolling coverage; define behavior where no implementation exists.

## 9. Search and export — P1/P2

- [ ] **Partial:** Unify search-result navigation and rendered highlighting so
  matching rules agree across Unicode and soft-wrapped lines.
- [ ] **Partial:** Previous-result controls and case/whole-word
  options and optional regex search with useful error reporting.
  Search-selection landed in 0.7.0.
- [ ] **Partial:** Distinguish the active result from other matches and expose
  current-result position/count and wraparound state accessibly.
- [ ] **Partial:** Move expensive search work off the UI path with cancellation;
  current engine search runs synchronously under the terminal lock.
- [x] **Implemented 0.10.0:** Styled screen/selection HTML and ANSI exports plus optional
  open-after-export using the default Windows application.
- [ ] **Partial:** Add cancellation, capacity handling and actionable IO errors for
  large search/export operations.

## 10. Tabs, splits, windows and sessions — P1/P2

- [ ] **Partial:** Reorder tabs by dragging, detach tabs into new windows
  and move tabs/panes between windows without replacing live sessions.
  Keyboard/action tab reordering landed in 0.7.0.
- [x] **Implemented 0.8.0:** Keyboard split-divider resizing. Zoom/unzoom and
  equal-area split equalization landed in 0.7.0.
- [x] **Implemented 0.7.0:** Spatial split navigation selects the visually
  adjacent pane instead of cycling the pane list.
- [ ] **Partial:** Tab overview, top/bottom placement, visibility, title length and
  close-button policies landed in 0.10.0. Richer pane/window title templates remain.
- [ ] **Partial:** Reopen closed tab recipes with fresh shells landed in 0.10.0;
  persistent undo and closed-pane reopening remain.
  destroyed ConPTY processes cannot simply be resumed.
- [x] **Implemented 0.7.0:** Preserve/restore pre-fullscreen window style, bounds,
  resize mode and state.
- [x] **Implemented 0.8.0:** Window-switching actions. Always-on-top, decorations,
  reset-window-size and focus-follows-mouse landed in 0.7.0.
- [ ] **Partial:** Persist monitor identity/position and restore safely when monitor
  topology, DPI or work areas change.
- [ ] **Missing:** Multi-window workspace collections and named workspace management;
  current saves restore only the most recently saved window.
- [ ] **Decision:** Optional scrollback persistence/crash recovery, including storage
  format, privacy, retention and corrupt-data handling; live process restoration
  is not implied by layout restoration.
- [ ] **Missing:** Single-instance routing and CLI requests to open a tab/window
  or launch a command in a selected existing instance.
- [x] **Implemented 0.12.0:** Per-window tab/pane limits can be lowered within the 20-tab/64-pane hard workspace bounds. New tab, split, duplicate and reopen paths honor them; capacity notices remain visible with the status bar hidden. Existing restored sessions are preserved.

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
- [x] **Implemented 0.11.0:** Windows CLI flags for command, directory, profile,
  title, dimensions, maximize/fullscreen, help and diagnostics, with transient
  launch and explicit process-command-line semantics.
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

- [x] **Implemented 0.13.0:** Inspector exposes live modes/cursor/render/geometry, feed timing/bytes, input/queue/cache counters, bounded unknown-protocol events and command availability. Full CPU/GPU/native allocator profiling is not claimed.
- [ ] **Partial:** Add bounded/rotated production crash reports and deliberate crash-data collection policy. Structured diagnostic exports, artifact hashes, ABI metadata and bounded opt-in protocol capture are implemented. The unhandled-exception errors.log is still an unbounded text log; it is not a structured crash-report subsystem.
- [x] **Implemented 0.13.0:** Palette/CLI diagnostics export identifies artifacts/ABI/platform, unsupported JSON keys and protocol coverage, preserving partial facts on pane snapshot failures; no default payload/screen/settings/environment capture.
- [x] **Documented 0.13.0:** README, AUDIT and VERIFICATION document shell/settings/renderer/clipboard/export recovery, native-fault limits and unsupported operations. Additional failure injection remains V22; broad interoperability remains open.

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
- [ ] **Partial:** Expand VERIFICATION.md's 28-case ledger into step-by-step manual scripts and record real browser/clipboard/IME/accessibility/device executions. The ledger and automated runner already exist; physical tests have not been signed off.

## 15. Build, packaging and release — P0/P1

- [ ] **Missing:** Windows-specific CI that builds and runs real frontend tests;
  inherited upstream workflows are disabled in the public fork until a deliberate Windows CI/release configuration is ready.
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
- [x] **Published 2026-11-01:** Registered a public fork of ghostty-org/ghostty with the Windows source/history and MIT attribution preserved. This is an independent community port, not an official Ghostty Windows release. The prior standalone development repository is retained privately as a backup.

## 16. Scope decisions and release exit criteria

- [ ] **Decision:** Classify upstream macOS/GTK/X11/Wayland-only options/actions as
  Windows equivalents or intentionally inapplicable, rather than copying them blindly.
- [ ] **Decision:** Set an explicit target for "feature complete": feature matrix,
  supported configurations, interoperability evidence and accepted exclusions.
- [x] **Reconciled 2026-11-01:** Checklist matched to the local 0.13.0 source and recorded verification evidence. Keep updating it as work lands; ongoing maintenance is not an unfinished terminal feature.
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
- [x] **Verified 2026-11-01 (V23):** Windows production-archive compression/reclamation is supported on the tested host; incremental/full passes reduce resident page storage and restore identical text. Frontend scheduling remains inactive. See [verification evidence](windows/VERIFICATION.md).
- [x] **Verified 2026-11-01 (V24):** Reviewed the pinned screen/page/selection/reflow/formatter TODOs, distinguished maintenance debt from observed behavior, and retained specific regression risks. See [verification evidence](windows/VERIFICATION.md).

- [ ] **Missing - engine (verified gap):** DECFRA rectangular fill and DECERA rectangular erase are ignored by the pinned compiled engine. Real-byte negative regression cases record unchanged target cells; do not advertise these as supported. See V26 in windows/VERIFICATION.md.

## 18. VT engine-to-Windows integration gaps — P1

The engine already exposes these capability families in
[terminal.h](include/ghostty/vt/terminal.h). Remaining work is to configure,
consume or validate them in our adapter/renderer, not to invent another parser.
Existing related backlog items remain in their owning sections.

- [x] **Implemented 0.13.0 - integration:** ENQ/XTVERSION identify ghostty-windows and its frontend version; TERM_PROGRAM_VERSION matches. Audited DA defaults retain VT220/ANSI color only and no Sixel/clipboard claims; xterm-256color remains the compatibility terminfo identity.
- [ ] **Missing — integration:** Connect host size-query and window-resize
  requests to measured cell/pixel/window geometry and a defined resize policy.
- [x] **Implemented 0.13.0 - integration:** Queries reflect the actual terminal palette; mode 2031 receives changed-theme reports outside the native lock. Auto themes follow OS changes; duplicate/disabled reports are suppressed.
- [ ] **Missing — integration:** Bridge the engine's glyph protocol into usable
  Windows font/glyph rendering; enabling a protocol flag alone is insufficient.
- [ ] **Partial — integration:** Audit OSC text-sizing and other rendering metadata
  against the C render API and WPF output; the current cell snapshot does not
  establish complete support for every engine-side text representation.
- [x] **Implemented 0.13.0 - integration:** Opt-in native unknown OSC/APC capture with 32x256-byte ring, truncation/eviction accounting and Base64 display. Capture is RAM-only; payloads are excluded from default exports and never executed.
- [ ] **Missing — integration:** Surface engine program-status and hierarchical/
  semantic state where applicable, with typed ownership and explicit unknowns.
- [x] **Verified 2026-11-01 (V25):** Terminal continuation/checkpoint APIs are not used for persistence. Workspace restoration starts fresh processes; drawing snapshots are not recovery checkpoints. See [verification evidence](windows/VERIFICATION.md).

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

## Delivery history (completed work)

Historical batch counts describe features delivered at the time. They are not
additional open tasks and should not be added to the current backlog counts.

### Completed in Preview 0.13.0 and verification follow-up

Nine previously missing groups were delivered: custom single-chord bindings and
precedence; configurable terminal send actions; configuration and action support
inventories; live inspector; diagnostics export; host identity; terminal theme
reports; bounded unknown OSC/APC diagnostics. Unbind advanced the broader binding
semantics group; sequences, tables, chains and global behavior remain.

The verification pass closed V23 (production compression capability), V24
(source TODO audit) and V25 (checkpoint/persistence applicability). It fixed the
ConPTY read/dispose shutdown race and replaced process-created assumptions in a
workspace test with observed command readiness. 442 regression checks, 71 extended
checks and the production compression probe passed. Hardware, cross-machine,
long-duration and broader conformance/fault coverage remains in the 25 Verify
items, with detailed requirements in windows/VERIFICATION.md.

### Partial-completion pass — Preview 0.12.0

This pass closes **15 of the 62 partial entries**, leaving **47 partial entries**.
It prioritizes ABI validation, pane recovery, file-drop correctness, settings
validation, clipboard/paste behavior, cursor/layout controls, mouse policies,
primary selection, palette coverage and capacity feedback. Other partial entries
were improved without being marked complete: native formatter errors now preserve
error categories; input/output pipe failures are surfaced; native word/repeat-click
options and vertical background alignment are exposed. Remaining native calls,
cleanup-failure paths, semantic gestures and animated backgrounds still need work.

The process-local primary buffer is a deliberate Windows equivalent, not a
system-wide X11 primary clipboard. File-drop quoting applies to standalone
arguments: CMD %/! spellings are refused; WSL requires POSIX-compatible shell
syntax and uses the default distribution's wslpath rather than assuming /mnt/c.
Custom single-chord mappings landed in 0.13.0; global/key-table semantics remain missing.
Renderer recovery covers managed failures, not unsafe native memory faults or
process-wide allocation exhaustion. Physical hover/device/clipboard interoperability
checks remain in their Verify entries. No broad VT conformance claim is made.

### Completed in Preview 0.11.0 — fifth batch of 20

| Done | Feature | Access |
| --- | --- | --- |
| [x] | CLI shell-profile selection | --profile |
| [x] | CLI startup directory | --directory |
| [x] | CLI custom process command line | --command |
| [x] | CLI initial tab title | --title |
| [x] | CLI window dimensions | --width / --height |
| [x] | CLI maximized startup | --maximize |
| [x] | CLI fullscreen startup | --fullscreen |
| [x] | CLI help window | --help |
| [x] | CLI version/platform diagnostics window | --diagnostics |
| [x] | Background image selection and rendering | Settings picker/path |
| [x] | Background image opacity | Settings |
| [x] | Background Cover/Contain/Stretch/Center | Settings |
| [x] | Background horizontal alignment | Settings |
| [x] | Background image tiling | Settings |
| [x] | Automatic settings-file reload | Settings |
| [x] | Configurable reload debounce | Settings |
| [x] | Nonfatal reload error notice and recovery | Automatic; click notice to dismiss |
| [x] | Open working directory in Explorer | Palette |
| [x] | Copy working directory | Palette |
| [x] | Open Windows on-screen keyboard | Palette |

CLI overrides open one fresh tab and do not save preferences/workspace on close.
Custom commands are Windows process command lines, not implicitly shell code;
select cmd.exe, PowerShell or wsl.exe explicitly for shell syntax. Commands are
transient and are not restored as persistent custom shell profiles. Help and
diagnostics are GUI windows, not stdout contracts. Custom commands skip welcome
bootstrapping. Image decoding is bounded to 64 MiB source/32 million pixels and
normalized to BGRA32; GIF/multi-frame files display only their first frame.
Vertical image positioning and animated backgrounds remain outstanding. Background
files are refreshed when settings are applied, not continuously watched.

Automatic reload is opt-in, debounced and shared by windows with one settings
owner. It validates one captured JSON document, preserves live workspace state,
shows errors without applying malformed edits, and clears reload errors on repair.
It does not merge concurrent edits, install an upstream text-config parser or
watch directories recursively. Explorer and keyboard-launch UI paths remain
manual verification scope. See windows/AUDIT.md for the accompanying cleanup.

### Completed in Preview 0.10.0 — fourth batch of 20

| Done | Feature | Access |
| --- | --- | --- |
| [x] | Styled HTML selection export | Palette |
| [x] | Styled HTML visible-screen export | Palette |
| [x] | ANSI/VT exports for screen, selection and scrollback | Palette |
| [x] | Formatted HTML selection clipboard with plain-text fallback | Palette |
| [x] | Configurable copied trailing-whitespace policy | Settings |
| [x] | Configurable Windows/LF copied line endings | Settings |
| [x] | Copy visible screen | Palette |
| [x] | Copy all scrollback | Palette |
| [x] | Paste as a single line | Palette |
| [x] | Optional open-after-export | Settings |
| [x] | Automatic/always/never tab-strip visibility | Settings |
| [x] | Top/bottom tab-strip placement | Settings |
| [x] | Tab overview with keyboard activation | Palette |
| [x] | Configurable Unicode-safe tab title length | Settings |
| [x] | Optional tab close buttons | Settings |
| [x] | Optional middle-click tab close | Settings |
| [x] | Duplicate tab layout with fresh shells | Palette |
| [x] | Reopen closed tab layout with fresh shells | Ctrl+Alt+Shift+T / palette |
| [x] | Command-palette page/first/last keyboard navigation | Ctrl+PageUp/PageDown/Home/End |
| [x] | Dedicated bold-italic font family | Settings |

Preferences are under **Appearance, scrolling, clipboard and tabs**. Reopen keeps
at most ten tab recipes per live window; it preserves layout, name and current
supported working directories, creates new identities and processes, and does not
restore closed processes, output or zoom. Closing the last window discards its
recipes. Duplicate/reopen respect workspace limits. ANSI exports include native
palette and styling sequences; HTML exports include the native palette CSS.
Formatted clipboard data supplies CF_HTML UTF-8 offsets plus plain text; receiving
application CSS support varies and physical clipboard interoperability remains a
manual check. Export opening is opt-in and uses the file's default Windows
application, which may be a browser for HTML. Single-line paste replaces line
breaks with spaces; it does not quote shell metacharacters or make a command safe.
Normal text-field Home/End/Page navigation remains available without Ctrl.

### Completed in Preview 0.9.0 — third batch of 20

These are supported frontend capabilities from the outstanding action, rendering
and preference groups below; completing them does not complete those entire groups.

| Done | Feature | Access |
| --- | --- | --- |
| [x] | Configurable line-scroll commands, up/down | Palette; Settings line count |
| [x] | Configurable fractional-page scrolling, up/down | Palette; Settings fraction |
| [x] | Extend selection to either line boundary | Ctrl+Alt+Shift+Home/End |
| [x] | Extend selection to screen boundaries | Ctrl+Alt+Home/End |
| [x] | Extend selection by pages | Ctrl+Alt+Shift+PageUp/PageDown |
| [x] | Focused cursor shape override | Settings |
| [x] | Explicit cursor color | Settings |
| [x] | Cursor opacity | Settings |
| [x] | Cursor blink enable/timing | Settings |
| [x] | Unfocused cursor shape/visibility | Settings |
| [x] | Selection background color | Settings |
| [x] | Search highlight background color | Settings |
| [x] | Inactive pane content dimming | Settings |
| [x] | Cell width multiplier | Settings |
| [x] | Cell height multiplier | Settings |
| [x] | Horizontal terminal padding | Settings |
| [x] | Vertical terminal padding | Settings |
| [x] | Automatic light/dark terminal themes | Settings; Windows personalization |
| [x] | Separate bold font family | Settings |
| [x] | Separate italic font family | Settings |

The new preferences live under **Cursor, typography, themes and scrolling**.
Selection boundaries use the native engine's screen/current-line semantics; they
are not history-wide selection shortcuts. Native applications may hide the cursor;
shape overrides do not force a hidden cursor visible. Blink is a frontend preference,
not a claim of full application-controlled blink-mode parity. Color fields use six
hexadecimal digits. Separate font families must be installed; blank inherits the
regular family, and bold takes priority when bold and italic are combined. Cell
multipliers change cell geometry, not glyph shaping or font size. Padding is shared
by text, images, selection, links and reported mouse coordinates. Inactive dimming
applies to pane contents, preserving the background material.

### Completed in Preview 0.8.0 — second 20-feature Windows batch

The following are 20 additional usable features. Grouped TODOs below remain
open when their broader scope is not yet complete.

| Done | Feature | Entry point |
| --- | --- | --- |
| [x] | Keyboard resizing of the nearest split divider | Ctrl+Alt+Shift+H/J/K/L |
| [x] | Cycle terminal windows, restoring minimized state | Ctrl+Alt+PageUp/PageDown |
| [x] | Scroll to the current selection | Scroll to selection |
| [x] | Scroll to an absolute history row | Scroll to row |
| [x] | Previous/next shell prompt navigation | Previous/Next shell prompt |
| [x] | Extend a selection by keyboard, or start it at the caret | Ctrl+Alt+Shift+arrows |
| [x] | Styled HTML scrollback export | Export styled scrollback as HTML |
| [x] | Open the owned Windows settings JSON file | Open settings JSON |
| [x] | Reload preferences from disk without replacing sessions | Reload settings from disk |
| [x] | Reset known preferences, keeping sessions and unknown fields | Reset preferences to defaults |
| [x] | Export preferences separately from workspace state | Export preferences |
| [x] | Import preferences with backup, keeping the live workspace | Import preferences |
| [x] | Configurable wheel speed with sub-notch accumulation | Settings: wheel lines |
| [x] | Hide pointer while typing, restore on mouse interaction | Settings: hide pointer |
| [x] | Configurable local right-click behavior | Settings: right-click action |
| [x] | Configurable local middle-click behavior | Settings: middle-click action |
| [x] | Clear selection after a successful copy | Settings: clear after copy |
| [x] | Configurable clear/retain selection on typing/paste/drop | Settings: clear on typing |
| [x] | Configurable scrollback byte limit, including off/unlimited | Settings: scrollback bytes |
| [x] | Configurable image storage byte limit, including off/unlimited | Settings: image bytes |

Prompt navigation requires OSC 133 shell markers; this does not install shell
integration. Divider keys move the nearest matching divider by five percentage
points, bounded to 5–95%, and are disabled while zoomed. Window activation remains
subject to Windows foreground rules. HTML export uses the engine formatter.
Preference operations use the existing Windows JSON format, not Ghostty's
upstream text configuration format. Null limits round-trip explicitly; omitted
limits retain 64 MiB defaults. Zero limits disable the corresponding storage
(zero image storage disables Kitty graphics). Changes can discard retained data.
Native picker/editor launch and physical clipboard interactions retain manual
verification requirements; automated checks do not claim those were exercised.

### Completed in Preview 0.7.0 — 20-feature Windows batch

These are 20 separately usable features drawn from the grouped items below.
Compound backlog items remain open where only part of their scope landed.
Use **Ctrl+Shift+P** to find actions; the palette shows availability, shortcuts
and toggle state. Implementation: [command catalogue](windows/WindowCommands.cs).

| Done | Feature | Entry point |
| --- | --- | --- |
| [x] | Full terminal reset, including history and modes | Reset terminal |
| [x] | Clear visible terminal screen while retaining history/modes | Ctrl+Shift+K |
| [x] | Per-pane read-only input mode | Ctrl+Shift+L |
| [x] | Per-pane application mouse-reporting override | Application mouse reporting |
| [x] | Reset font size to the loaded/last-applied configured size | Ctrl+Shift+0 |
| [x] | Validated set-font-size action with shared-pane refresh | Set font size |
| [x] | Scroll to the top of history | Ctrl+Shift+Home |
| [x] | Scroll to the current bottom | Ctrl+Shift+End |
| [x] | Copy full effective tab title | Copy tab title |
| [x] | Export the visible viewport to plain text | Export visible screen |
| [x] | Export just the selection to plain text | Export selection |
| [x] | Search the selected text | Search selected text |
| [x] | Reorder tabs by keyboard/action without replacing sessions | Ctrl+Shift+PageUp/PageDown |
| [x] | Zoom/unzoom the active split, retaining the layout | Ctrl+Shift+Z |
| [x] | Equalize nested split areas | Equalize split areas |
| [x] | Navigate to spatially adjacent panes | Ctrl+Alt+arrows |
| [x] | Toggle always-on-top | Always on top |
| [x] | Toggle native window decorations | Window decorations |
| [x] | Reset window size | Reset window size |
| [x] | Focus panes on hover in the active window | Settings / Focus follows mouse |

Fullscreen restoration was also corrected: exiting restores prior bounds,
window state, decoration style and resize mode. Font changes now have one
settings owner notifying all associated panes/windows. Clipboard failures are
reported rather than being presented as successful copies.

Modes and split zoom are per live pane/tab; they are not restored workspace
properties. Reset/clear act on terminal state, not the running shell process.
Exports are plain text. Drag tab reordering, tab detachment, styled export,
Keyboard split resizing landed in 0.8.0; configurable single-chord keybindings landed in 0.13.0.

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
