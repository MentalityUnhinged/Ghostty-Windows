# Windows frontend audit — 1 November 2026

Scope: the accumulated Windows frontend, CLI/settings composition, ConPTY owner,
clipboard/export path, background decoder, file watcher, native integration and
build/test/package gates. This is a targeted source and runtime audit, not a claim
of full upstream VT conformance, a security certification or physical-device QA.

## Corrected ownership and failure boundaries

- Settings drafts now detach mutable workspace trees and extension dictionaries,
  as well as event handlers and watcher ownership. Failed/cancelled edits cannot
  mutate those live representations through shared references.
- Config watchers are shared per settings owner with reference-counted window
  leases. They consume one captured JSON read, debounce replacements, preserve
  live workspace state and ignore queued callbacks after disposal. Watcher errors
  and malformed files have explicit nonfatal UI notices; repair clears the reload
  status without clearing an unrelated pane warning.
- Formatted clipboard plain/HTML data is captured under one engine lock.
  Clipboard-read contention is reported rather than escaping the UI handler.
- ConPTY start is single-use. Input is bounded by both message count and 4 MiB of
  outstanding bytes. Pipe failures close/drain the writer and report rejected
  input per pane. Cleanup awaits the process monitor before closing its handle,
  removing the wait/exit-query versus handle-close race.
- Background decoding checks compressed size before decode, bounds pixel count,
  normalizes to BGRA32, freezes the result and releases the source stream. Cache
  metadata avoids decoding again for unrelated settings changes. Missing/bad
  images remain recoverable. This is a metadata cache, not a cryptographic file
  identity guarantee or an animated-image implementation.
- Test dispatch now has one named registry. Focused suites no longer run unrelated
  batches, ambiguous test modes are rejected, and literal CLI option values cannot
  accidentally select a test mode. Packaging requires an explicitly full passing
  report plus matching runtime hashes; a focused pass cannot authorize a package.
- Build provenance separates the pinned engine base from the frontend commit,
  records dirty-source and engine-tree match state, and states whether the engine
  archive was rebuilt in this invocation. A cached archive hash is not represented
  as proof of a fresh build.
- Stale staging output was moved into the ignored artifact archive. Source,
  local evidence and upstream files were preserved.

## Validation

Focused integration coverage includes real custom-command launch, independent
ConPTY operation, duplicate-start rejection and monitor settlement; concurrent
clipboard reads during output; queue byte limits; detached settings edits; image
composition and handle release; external config repair and watcher disposal.
The final full-suite result is shipped as `test-results.json`, bound to executable,
managed frontend and native adapter hashes. Physical clipboard/CSS interoperability,
Explorer/OSK activation, device input and OS-level visual behavior remain manual
coverage. Refer to PORTING.md for the broader matrix.

## Remaining work

- ABI negotiation and managed per-pane render/measurement recovery landed in
  0.12.0. A complete audit of discarded native result codes, native memory-fault
  containment and every decoder/resource-exhaustion path remain open.
- Reader-side IO failure reporting landed in 0.12.0. Exceptional native cleanup
  paths still need stronger retry semantics; OS handle-close failure injection has
  not been verified. This audit does not establish all resource-exhaustion behavior.
- Automatic config reload does not merge concurrent edits or parse upstream text
  config. Decoder metadata caching does not notice changes that preserve both
  file length and timestamp until the cache is invalidated.
- IME geometry, accessibility text-provider support, terminal delegation/default
  registration, signing/installer/CI and remaining VT integration gaps in TODO.md
  are not resolved by this cleanup.

No upstream source files were deleted or rewritten as part of this audit.

## 0.12.0 follow-up

Native ABI/version/size/capability checks now run before callback registration.
Formatter failure categories, size-based string readback and application cursor
blink metadata are preserved. The upstream C terminal still bounds titles to 1024
bytes; the host does not claim to remove that engine limit. Shell-file insertion
uses explicit shell contracts and live WSL path translation, with cancellable
GUI-safe standard IO. Managed renderer recovery was verified with an injected font
failure and recovery of the same pane; ordinary shell-start failure was exercised
without closing the window. Native memory faults are deliberately not swallowed.

Focused test runs now initialize a fresh failing/running report, and dispatcher
failures write their own failed report. This prevents an older passing result from
being mistaken for the result of a crashed test process. Build/package gates still
require a full passing report and matching hashes. The command catalogue now uses
reference identity for mutable availability notifications; shortcuts use one chord
owner for both matching and labels instead of parallel hardcoded window switches.

The 0.12.0 mixed-build probe also launched the new frontend with the previous
native DLL in an isolated staging directory. It failed before terminal allocation
with the expected older/mismatching-build recovery message. Current native and
managed artifacts were left unchanged.


## 0.13.0 follow-up

Binding validation owns normalized chords once. Detached editor lists and live
apply preserve that representation. Palette labels share effective bindings
with dispatch; recovery chords remain reachable. Unbind bypasses pane copy/paste
fallback. Search Escape retains UI ownership in terminal-first mode.

Native ABI is version 2 with host identity/diagnostics capability bits. Identity
is configured before reads. Scheme replies use the upstream encoder and leave
the Engine lock before transport callbacks. Unknown OSC/APC bytes are copied
before borrowed callback data expires, bounded at the producer to 32 events of
256 bytes per pane. Opt-out erases samples. Default exports omit payloads and
preserve other facts on snapshot failures. Reports preserve the prior file as
.backup; no environment, terminal screen or typed contents are serialized.

The inventory generator masks literals/comments before counting declaration
scope and binds output to source hashes. Windows equivalents are marked partial,
not upstream config/action payload parity. Earlier audit limitations still apply:
native memory-fault containment, complete result-code audit, real performance
and physical keyboard/IME verification remain outstanding.


## Verification harness follow-up

The bounded verification runner exercises real ConPTY applications and a native
compression probe against the production archive. Process observations dispose
their own handles and retain a HANDLE-backed identity across teardown; PID lookup
after teardown is not used as proof. Test session cleanup precedes diagnostic
file IO, so a denied/full output directory cannot bypass child/pump settlement.
Native compression reports and formatted content comparison establish actual
Windows runtime behavior rather than relying on simulated Zig test reclamation.

Extended evidence is bound to the same three runtime hashes as full regression
evidence. Rebuilds invalidate old reports; an attempted extended run removes the
previous success summary first. Packaging rejects mismatched extended evidence.
These results do not close the physical-device, cross-machine, long-duration,
independent conformance or remaining fault-injection cases in VERIFICATION.md.


The extended run reproduced a real read/dispose race. PseudoConsole now closes
its native pipe peers, awaits both pumps and the process monitor, then disposes
host streams and its process handle. It does not add a catch that hides a torn
FileStream. Natural-exit/in-flight-read stress covers the ordering. A separate
workspace test now uses actual command completion as readiness, excluding a
welcome-bootstrap child that could consume early test input.
