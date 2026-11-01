# Windows verification matrix

Verification pass: 1 November 2026. Target: the locally built Windows Preview
0.13.0 and pinned engine `d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`.
The IDs below retain the order of the original 28 Verify entries in TODO.md.
**All 28 are accounted for; this is not a claim that all 28 passed.**

## Reproduce

```powershell
.\windows\verify.ps1
```

This builds/runs the full regression suite, runs the bounded verification suite,
and links a production compression probe against the exact engine archive
identified by the build manifest. `-UseCurrentBuild` requires a matching passing
full-suite report. Artifacts are under `windows/artifacts/verification/`:
`result.json`, `verification-metrics.json`, `compression.json`, `summary.json`,
and controlled application `.txt`/`.vt` traces. Successful reports bind checks to
EXE, managed DLL and native DLL hashes. A nonzero process exit is a failed run,
regardless of any older report. Failed focused runs retain their completed checks.

`--test-real-apps` isolates the application cases while debugging; it is not a
substitute for the complete verification run. Programs use their real ConPTY
transport, compiled VT engine and render-cell snapshots. Vim is started without
user configuration, swap or viminfo; tmux uses a new isolated socket and no user
configuration. Linux interactive programs have a timeout and kill grace period.
Tests never attach to the user's tmux session.

The available host is Windows 11 Pro build 26200, Intel UHD Graphics 620,
with PowerShell 5, PowerShell 7 and one Ubuntu WSL distribution. This establishes
neither Windows 10 coverage nor a multi-GPU, mixed-DPI/HDR or multi-distribution
test matrix. Environment evidence is `artifacts/verification-environment.json`.

## Case ledger

| ID | Original verification task | Evidence/status | Remaining work before closure |
| --- | --- | --- | --- |
| V01 | Close/reopen, exits, failures, cancellation, Job Object cleanup | Partial: 20 real owned-process close/settlement cycles, ten natural-exit/in-flight-read races and five failed launches; natural exits in real application cases; prior public-boundary tests | Descendant-process identity checks, Linux-side orphan checks and cancellation/failed-cleanup fault cases |
| V02 | Resize/screen switches/eviction during selection, images, synchronization, search | Partial: 150 combined selection/search/sync/resize/screen-switch cycles, bounded-history output; existing image chunk/reflow tests | Concurrent image transfers and selection during actual eviction, randomized scheduling |
| V03 | Focus/tab/split changes with held input, composition, capture, menus | Partial: existing window/layout and key-state tests | Physical held modifiers, active composition and capture/menu transitions together |
| V04 | Real Japanese/Chinese/Korean IMEs | Requires real IMEs | Compose/convert/cancel/commit in each IME; candidate window at caret, split/DPI/focus transitions; record OS/IME versions |
| V05 | Physical layouts and held-key layout changes | Requires physical layout testing | US/UK, AltGr, AZERTY/QWERTZ/Cyrillic; character and shortcut results, layout changes with held keys |
| V06 | Modifier sides, locks, keypads, unusual/RDP input | Requires devices/RDP | Press/repeat/release traces and application state for every device/layout/lock combination |
| V07 | Real Kitty keyboard interoperability | Partial: native negotiation fixtures and real Vim/tmux smoke runs | Real Kitty-negotiating applications, push/pop, releases/repeats through nested/remote sessions; Vim smoke alone does not prove Kitty negotiation |
| V08 | Legacy keyboard modes in real applications | Partial: real Vim/less and native application-cursor mode save/restore/query | Explicit application-keypad and modifyOtherKeys applications and nested-session cases |
| V09 | Mixed DPI, HDR, emoji/boxes, long-running memory | Partial: native DEC box drawing plus existing font/render cases | Move live panes between real DPI/HDR displays; record monitor/profile/scaling; overnight memory/cursor/selection/image checks |
| V10 | Malformed images, failure, eviction and transitions | Partial: existing PNG/zlib/chunk/eviction tests and repeated malformed image recovery | Allocator fault injection, concurrent chunks, cancellation/reflow/eviction scheduling and large payload pressure |
| V11 | Real image tools/multiplexers | Open | Use actual Kitty image producers through direct/remote/tmux paths; compare pixels/geometry/alpha/lifetime against a reference. Existing direct-engine fixtures do not establish ConPTY image passthrough |
| V12 | Physical selection, capture, browser and clipboard contention | Partial: native selection and clipboard/public-boundary cases | Physical capture loss/autoscroll, browser activation and clipboard contention across real applications/displays |
| V13 | Touchpad/high-resolution wheel/touch/pen/horizontal | Partial: fractional wheel tests and explicit scroll policy | Real hardware gestures, horizontal axis, capture and limits; touch/pen behavior remains unverified |
| V14 | Pickers/taskbar/notifications/focus across Windows versions | Partial: actual Windows Shell extraction verifies icons | Real pickers/focus/taskbar behavior on each supported OS; notification features still missing |
| V15 | Screen readers, magnification, text/motion/contrast | Requires accessibility interaction | Narrator plus another reader, real high contrast, magnification and reduced motion. A terminal text UIA provider is still missing; do not claim readable terminal text |
| V16 | Sustained throughput/UI/CPU/GPU/bounded memory | Partial: measured 40,000-row Unicode/color burst, feed/capture timing and process private bytes | Multi-hour live WPF UI latency/CPU/GPU/image/selection benchmark and memory plateaus. Burst counters are observations, not a performance acceptance threshold |
| V17 | Long-lived sessions/UI churn/exhaustion/settings/shutdown | Partial: bounded lifecycle/load and existing transactional settings tests | Soak duration, repeated real UI/window churn, failure injection and shutdown under concurrent load |
| V18 | Windows 10/11 cross-machine matrix | Requires other machines/OS installations | Record exact OS/GPU/driver/DPI/input/filesystem/permissions/shell cells; run automation plus physical cases on each |
| V19 | PowerShell 5/7, CMD, multiple WSL distributions/shells | Partial: actual PS5/PS7/CMD and Ubuntu bash/sh sessions | Additional distributions/shells and profile configurations. One Ubuntu distribution is not multiple-distribution coverage |
| V20 | Editors/pagers/finders/REPLs/multiplexers/remote | Partial: real Vim, less, Python REPL, tmux; captured screen state and successful exits | Fuzzy finder, mouse interactions, explicit keyboard negotiation and remote/nested paths |
| V21 | VT conformance suites and host integrations | Partial: existing host callback tests plus new real-byte state/reply cases | Run an independent conformance suite; report each applicable test and unsupported callback separately |
| V22 | Cancellation/malformed/capacity/identity/cleanup/permissions | Partial: existing capacity/identity/transactional error cases and new malformed/lifecycle tests | Real ACL denial and failed-cleanup injection, cancellation across every advertised public boundary |
| V23 | Windows scrollback compression/reclamation | Verified for this production archive/host; see below | Other Windows machines remain V18; frontend idle compression scheduling is not implemented |
| V24 | Review source TODOs/boundary risks | Review complete; see source audit below | Specific untested risks remain explicitly assigned to V02/V10/V26; review completion does not mean all core tests exist |
| V25 | Continuation/checkpoint/snapshot persistence applicability | Verified not used for terminal recovery/persistence | Reopen if terminal-state persistence is introduced; current workspace restoration starts fresh processes |
| V26 | Mode/query/device/status/capability/window/charset/rectangle streams | Partial: real private mode save/restore/query, cursor report, DEC charset and negative rectangle tests; existing DA/version/color cases | Capability/window-operation matrices and broader mode/charset tests. DECFRA/DECERA are confirmed missing |
| V27 | Every advertised capability vs compiled host | Partial: build hashes/ABI/real callbacks and support matrices | Complete claim-by-claim acceptance audit, including actual image producer transport and each optional callback; ABI bits are not VT conformance |
| V28 | Differential/reference-terminal conformance | Open | Run identical controlled applications/streams in independent reference terminals; compare state, pixels and replies. Comparing the Windows bridge with the same engine is not independent differential evidence |

## Production compression result (V23)

The standalone C probe uses the production archive, not `zig test`: the latter
deliberately simulates reclamation and cannot establish real Windows support.
It queries memory usage and activity, runs incremental compression to completion
then a full pass, snapshots/restores history and compares every formatted byte.

Observed: `compressionSupported=true`, complete result 2, two incremental calls,
one compressed page; resident page storage 802,816 -> 406,269 bytes; 57,903
terminal-content bytes compared equal after restoration. These are native page
accounting values, not process RSS or an assertion that every page compresses.
The frontend currently does not schedule this compression: supported in the
engine and inactive in the frontend are distinct from unsupported.
The probe also checks that an unsupported runtime returns the unsupported result.

## Pinned source TODO audit (V24)

Reviewed `Screen.zig`, `page.zig`, `Selection.zig`, `PageList.zig` (including
reflow) and `formatter.zig`; ten TODO annotations occur in these files.

| Source location/owner | Interpretation and observable verification consequence |
| --- | --- |
| `Screen.zig`, two hyperlink/grapheme capacity-growth questions | Allocation/performance policy questions; not proof of a missing terminal operation. Malformed/capacity and allocation-failure coverage belongs to V10/V22 |
| `Screen.zig:cursorMarkDirty` | Explicit unit-test gap. Existing cursor rendering checks cover basic observable behavior; a complete mutation/dirty-row matrix is still V26 |
| `Screen.zig:selectWordBetween` | Explicit test gap. Native double/triple-click and wrapped-line cases exist; reverse word drag across reflow/evicted pages remains V02 |
| `Screen.zig:cursorResetWrap` documentation (not a TODO tag) | Documents scrolling-region awareness as an edge-case limitation. Preserve it as a regression risk; the comment alone does not establish a reproduced Windows defect |
| `page.zig` hyperlink `addWithId` performance assumption | Optimization question, not missing hyperlink support. Existing OSC8 identity/readback cases remain relevant |
| `PageList.zig`, documentation TODO and deferred offscreen reflow | Documentation/performance debt. Resize/history coverage does not establish all offscreen style/link/selection outcomes; retain V02/V10 |
| `PageList.zig:Pin.leftWrap/rightWrap` | Missing unit tests for wrapping across pages of different widths. Add variable-width page/reflow boundary cases; do not claim every path is covered by a resize smoke test |
| `formatter.zig:PageFormatter` palette helper | Low-level snapshot helper TODO. The Windows terminal formatter requests palette definitions and existing ANSI export tests cover that host path; not evidence that Windows ANSI export is absent |

Separate compiled-engine finding: DECFRA (`CSI Pc;Pt;Pl;Pb;Pr $ x`) leaves
requested cells unfilled, and DECERA (`CSI Pt;Pl;Pb;Pr $ z`) leaves populated
target cells unchanged. Negative regression cases preserve these as explicit
unsupported behavior; passing those checks is **not** an implementation claim.

## Persistence applicability (V25)

`Workspace` serializes pane/tab identities, profiles, directories, geometry and
focus. `MainWindow` restores this recipe into fresh `TerminalView`/ConPTY owners.
`WorkspaceTests` exercises that round trip. The bridge calls no terminal
checkpoint/continuation serialization or restoration API. Render frames and
synchronized image snapshots are used for drawing only; they are not persistent
terminal-state checkpoints. No shell/history/process resurrection is claimed.


## Failures found and corrected during this pass

- Real shutdown race: PseudoConsole disposed its output FileStream before its
  read pump had settled. A normal close could therefore fault Cleanup with
  `NotSupportedException: Stream does not support reading`. Closing ConPTY now
  ends its pipe peers first; both pumps and the process monitor settle before
  their streams/process handle are disposed. Ten gated in-flight-read cases
  exercise this ordering without hiding the exception.
- Workspace-test readiness race: a created process did not prove that the CMD
  shell, rather than a welcome bootstrap child, would consume input. The
  split/close ownership test disables the welcome bootstrap and waits for an
  executed readiness command's output. It continues to verify the original
  shell's arithmetic result after closing a neighbor.

The handle samples distinguish ThreadPool warm-up from per-session growth;
resource counts remain observations, not an overnight leak certification.
No new physical-input or external-machine coverage is implied by these fixes.
