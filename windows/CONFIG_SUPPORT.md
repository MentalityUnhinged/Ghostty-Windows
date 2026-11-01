# Configuration option support matrix

Generated from `src/config/Config.zig` at engine base `d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`.
Source SHA-256: `a651b4aa0edb744b35b028ae7fc9cf2887227471665a3665979e9a5515c4d19c`. Inventory: **219 declarations**.

The upstream configuration language is not parsed by this application. Windows
preferences use JSON; Windows action IDs use the command palette/custom bindings.
**Partial: Windows equivalent** identifies a related implementation, not identical
upstream syntax, payloads, defaults or semantics. **Unsupported** means no exposed
Windows implementation is claimed. No entries are silently ignored or classified
as platform-inapplicable without a specific implementation/policy review.
See KEYBINDS.md and README.md for actual Windows contracts. Argument-bearing
upstream actions can require a dialog or a fixed preference on Windows.

Regenerate with `python windows/support-matrices.py`; verify with `--check`.

| Upstream name | Status | Windows equivalent / remaining work |
| --- | --- | --- |
| `language` | Unsupported | No exposed Windows implementation |
| `font-family` | Partial: Windows equivalent | FontFamily |
| `font-family-bold` | Partial: Windows equivalent | BoldFontFamily |
| `font-family-italic` | Partial: Windows equivalent | ItalicFontFamily |
| `font-family-bold-italic` | Partial: Windows equivalent | BoldItalicFontFamily |
| `font-style` | Unsupported | No exposed Windows implementation |
| `font-style-bold` | Unsupported | No exposed Windows implementation |
| `font-style-italic` | Unsupported | No exposed Windows implementation |
| `font-style-bold-italic` | Unsupported | No exposed Windows implementation |
| `font-synthetic-style` | Unsupported | No exposed Windows implementation |
| `font-feature` | Unsupported | No exposed Windows implementation |
| `font-size` | Partial: Windows equivalent | FontSize |
| `font-variation` | Unsupported | No exposed Windows implementation |
| `font-variation-bold` | Unsupported | No exposed Windows implementation |
| `font-variation-italic` | Unsupported | No exposed Windows implementation |
| `font-variation-bold-italic` | Unsupported | No exposed Windows implementation |
| `font-codepoint-map` | Unsupported | No exposed Windows implementation |
| `clipboard-codepoint-map` | Unsupported | No exposed Windows implementation |
| `font-thicken` | Unsupported | No exposed Windows implementation |
| `font-thicken-strength` | Unsupported | No exposed Windows implementation |
| `font-shaping-break` | Unsupported | No exposed Windows implementation |
| `alpha-blending` | Unsupported | No exposed Windows implementation |
| `adjust-cell-width` | Unsupported | No exposed Windows implementation |
| `adjust-cell-height` | Unsupported | No exposed Windows implementation |
| `adjust-font-baseline` | Unsupported | No exposed Windows implementation |
| `adjust-underline-position` | Unsupported | No exposed Windows implementation |
| `adjust-underline-thickness` | Unsupported | No exposed Windows implementation |
| `adjust-strikethrough-position` | Unsupported | No exposed Windows implementation |
| `adjust-strikethrough-thickness` | Unsupported | No exposed Windows implementation |
| `adjust-overline-position` | Unsupported | No exposed Windows implementation |
| `adjust-overline-thickness` | Unsupported | No exposed Windows implementation |
| `adjust-cursor-thickness` | Unsupported | No exposed Windows implementation |
| `adjust-cursor-height` | Unsupported | No exposed Windows implementation |
| `adjust-box-thickness` | Unsupported | No exposed Windows implementation |
| `adjust-icon-height` | Unsupported | No exposed Windows implementation |
| `grapheme-width-method` | Unsupported | No exposed Windows implementation |
| `freetype-load-flags` | Unsupported | No exposed Windows implementation |
| `theme` | Partial: Windows equivalent | Theme / AutoTerminalTheme |
| `background` | Unsupported | No exposed Windows implementation |
| `foreground` | Unsupported | No exposed Windows implementation |
| `background-image` | Partial: Windows equivalent | BackgroundImagePath |
| `background-image-opacity` | Partial: Windows equivalent | BackgroundImageOpacity |
| `background-image-position` | Partial: Windows equivalent | BackgroundImageAlignment / BackgroundImageVerticalAlignment |
| `background-image-fit` | Partial: Windows equivalent | BackgroundImageFit |
| `background-image-repeat` | Partial: Windows equivalent | BackgroundImageRepeat |
| `selection-foreground` | Unsupported | No exposed Windows implementation |
| `selection-background` | Partial: Windows equivalent | SelectionBackground |
| `selection-clear-on-typing` | Unsupported | No exposed Windows implementation |
| `selection-clear-on-copy` | Unsupported | No exposed Windows implementation |
| `selection-word-chars` | Unsupported | No exposed Windows implementation |
| `minimum-contrast` | Partial: Windows equivalent | MinimumTextContrast |
| `palette` | Unsupported | No exposed Windows implementation |
| `palette-generate` | Unsupported | No exposed Windows implementation |
| `palette-harmonious` | Unsupported | No exposed Windows implementation |
| `cursor-color` | Partial: Windows equivalent | CursorColor |
| `cursor-opacity` | Partial: Windows equivalent | CursorOpacity |
| `cursor-style` | Partial: Windows equivalent | CursorShape |
| `cursor-style-blink` | Partial: Windows equivalent | CursorBlink / FollowApplicationCursorBlink |
| `cursor-text` | Partial: Windows equivalent | CursorTextColor |
| `cursor-click-to-move` | Unsupported | No exposed Windows implementation |
| `mouse-hide-while-typing` | Partial: Windows equivalent | HideMouseWhileTyping |
| `scroll-to-bottom` | Unsupported | No exposed Windows implementation |
| `mouse-shift-capture` | Partial: Windows equivalent | ShiftMouseOverride |
| `mouse-reporting` | Unsupported | No exposed Windows implementation |
| `mouse-scroll-multiplier` | Unsupported | No exposed Windows implementation |
| `background-opacity` | Partial: Windows equivalent | BackgroundOpacity / BackgroundEffect |
| `background-opacity-cells` | Unsupported | No exposed Windows implementation |
| `background-blur` | Unsupported | No exposed Windows implementation |
| `unfocused-split-opacity` | Partial: Windows equivalent | InactivePaneOpacity |
| `unfocused-split-fill` | Unsupported | No exposed Windows implementation |
| `split-divider-color` | Unsupported | No exposed Windows implementation |
| `split-preserve-zoom` | Unsupported | No exposed Windows implementation |
| `search-foreground` | Unsupported | No exposed Windows implementation |
| `search-background` | Unsupported | No exposed Windows implementation |
| `search-selected-foreground` | Unsupported | No exposed Windows implementation |
| `search-selected-background` | Unsupported | No exposed Windows implementation |
| `command` | Unsupported | No exposed Windows implementation |
| `initial-command` | Unsupported | No exposed Windows implementation |
| `notify-on-command-finish` | Unsupported | No exposed Windows implementation |
| `notify-on-command-finish-action` | Unsupported | No exposed Windows implementation |
| `notify-on-command-finish-after` | Unsupported | No exposed Windows implementation |
| `env` | Unsupported | No exposed Windows implementation |
| `input` | Unsupported | No exposed Windows implementation |
| `wait-after-command` | Unsupported | No exposed Windows implementation |
| `abnormal-command-exit-runtime` | Unsupported | No exposed Windows implementation |
| `scrollback-limit-bytes` | Partial: Windows equivalent | ScrollbackLimitBytes |
| `scrollback-limit-lines` | Unsupported | No exposed Windows implementation |
| `scrollback-compression` | Unsupported | No exposed Windows implementation |
| `scrollbar` | Unsupported | No exposed Windows implementation |
| `link` | Unsupported | No exposed Windows implementation |
| `link-url` | Unsupported | No exposed Windows implementation |
| `link-osc8` | Unsupported | No exposed Windows implementation |
| `link-previews` | Unsupported | No exposed Windows implementation |
| `maximize` | Unsupported | No exposed Windows implementation |
| `fullscreen` | Unsupported | No exposed Windows implementation |
| `title` | Unsupported | No exposed Windows implementation |
| `class` | Unsupported | No exposed Windows implementation |
| `x11-instance-name` | Unsupported | No exposed Windows implementation |
| `working-directory` | Partial: Windows equivalent | WorkingDirectory |
| `keybind` | Partial: Windows equivalent | KeyBindings / ShortcutPrecedence |
| `key-remap` | Unsupported | No exposed Windows implementation |
| `window-padding-x` | Partial: Windows equivalent | PaddingHorizontal |
| `window-padding-y` | Partial: Windows equivalent | PaddingVertical |
| `window-padding-balance` | Partial: Windows equivalent | BalancePadding |
| `window-padding-color` | Unsupported | No exposed Windows implementation |
| `window-vsync` | Unsupported | No exposed Windows implementation |
| `window-inherit-working-directory` | Partial: Windows equivalent | new-tab-folder command (explicit action only) |
| `tab-inherit-working-directory` | Unsupported | No exposed Windows implementation |
| `split-inherit-working-directory` | Unsupported | No exposed Windows implementation |
| `window-inherit-font-size` | Unsupported | No exposed Windows implementation |
| `window-decoration` | Partial: Windows equivalent | decorations command (not a persisted preference) |
| `window-title-font-family` | Unsupported | No exposed Windows implementation |
| `window-subtitle` | Unsupported | No exposed Windows implementation |
| `window-theme` | Unsupported | No exposed Windows implementation |
| `window-colorspace` | Unsupported | No exposed Windows implementation |
| `window-height` | Unsupported | No exposed Windows implementation |
| `window-width` | Unsupported | No exposed Windows implementation |
| `window-position-x` | Unsupported | No exposed Windows implementation |
| `window-position-y` | Unsupported | No exposed Windows implementation |
| `window-save-state` | Partial: Windows equivalent | Workspace |
| `window-step-resize` | Unsupported | No exposed Windows implementation |
| `window-new-tab-position` | Unsupported | No exposed Windows implementation |
| `window-show-tab-bar` | Unsupported | No exposed Windows implementation |
| `window-titlebar-background` | Partial: Windows equivalent | OS dark/light title bar policy |
| `window-titlebar-foreground` | Unsupported | No exposed Windows implementation |
| `drag-handle` | Unsupported | No exposed Windows implementation |
| `resize-overlay` | Unsupported | No exposed Windows implementation |
| `resize-overlay-position` | Unsupported | No exposed Windows implementation |
| `resize-overlay-duration` | Unsupported | No exposed Windows implementation |
| `focus-follows-mouse` | Unsupported | No exposed Windows implementation |
| `clipboard-read` | Unsupported | No exposed Windows implementation |
| `clipboard-write` | Unsupported | No exposed Windows implementation |
| `clipboard-write-limit-bytes` | Unsupported | No exposed Windows implementation |
| `clipboard-trim-trailing-spaces` | Partial: Windows equivalent | CopyTrimTrailing |
| `clipboard-paste-protection` | Partial: Windows equivalent | PasteControlPolicy / confirmation policy |
| `clipboard-paste-bracketed-safe` | Unsupported | No exposed Windows implementation |
| `title-report` | Unsupported | No exposed Windows implementation |
| `image-storage-limit` | Partial: Windows equivalent | ImageLimitBytes |
| `copy-on-select` | Partial: Windows equivalent | CopyOnSelect |
| `right-click-action` | Unsupported | No exposed Windows implementation |
| `middle-click-action` | Unsupported | No exposed Windows implementation |
| `click-repeat-interval` | Partial: Windows equivalent | RepeatClickMilliseconds |
| `config-file` | Unsupported | No exposed Windows implementation |
| `config-default-files` | Unsupported | No exposed Windows implementation |
| `confirm-close-surface` | Partial: Windows equivalent | ConfirmClose |
| `quit-after-last-window-closed` | Partial: Windows equivalent | Current lifetime policy (always quit) |
| `quit-after-last-window-closed-delay` | Unsupported | No exposed Windows implementation |
| `initial-window` | Unsupported | No exposed Windows implementation |
| `undo-timeout` | Unsupported | No exposed Windows implementation |
| `quick-terminal-position` | Unsupported | No exposed Windows implementation |
| `quick-terminal-size` | Unsupported | No exposed Windows implementation |
| `gtk-quick-terminal-layer` | Unsupported | No exposed Windows implementation |
| `gtk-quick-terminal-namespace` | Unsupported | No exposed Windows implementation |
| `quick-terminal-screen` | Unsupported | No exposed Windows implementation |
| `quick-terminal-animation-duration` | Unsupported | No exposed Windows implementation |
| `quick-terminal-autohide` | Unsupported | No exposed Windows implementation |
| `quick-terminal-space-behavior` | Unsupported | No exposed Windows implementation |
| `quick-terminal-keyboard-interactivity` | Unsupported | No exposed Windows implementation |
| `shell-integration` | Unsupported | No exposed Windows implementation |
| `shell-integration-features` | Unsupported | No exposed Windows implementation |
| `command-palette-entry` | Unsupported | No exposed Windows implementation |
| `osc-color-report-format` | Unsupported | No exposed Windows implementation |
| `vt-kam-allowed` | Unsupported | No exposed Windows implementation |
| `vt-window-resize-allowed` | Unsupported | No exposed Windows implementation |
| `vt-xt-checksum-report` | Unsupported | No exposed Windows implementation |
| `vt-xt-checksum-extension` | Unsupported | No exposed Windows implementation |
| `custom-shader` | Unsupported | No exposed Windows implementation |
| `custom-shader-animation` | Unsupported | No exposed Windows implementation |
| `bell-features` | Unsupported | No exposed Windows implementation |
| `bell-audio-path` | Unsupported | No exposed Windows implementation |
| `bell-audio-volume` | Unsupported | No exposed Windows implementation |
| `app-notifications` | Unsupported | No exposed Windows implementation |
| `macos-non-native-fullscreen` | Unsupported | No exposed Windows implementation |
| `macos-window-buttons` | Unsupported | No exposed Windows implementation |
| `macos-titlebar-style` | Unsupported | No exposed Windows implementation |
| `macos-titlebar-proxy-icon` | Unsupported | No exposed Windows implementation |
| `macos-dock-drop-behavior` | Unsupported | No exposed Windows implementation |
| `macos-option-as-alt` | Unsupported | No exposed Windows implementation |
| `macos-window-shadow` | Unsupported | No exposed Windows implementation |
| `macos-hidden` | Unsupported | No exposed Windows implementation |
| `macos-auto-secure-input` | Unsupported | No exposed Windows implementation |
| `macos-secure-input-indication` | Unsupported | No exposed Windows implementation |
| `macos-applescript` | Unsupported | No exposed Windows implementation |
| `macos-icon` | Unsupported | No exposed Windows implementation |
| `macos-custom-icon` | Unsupported | No exposed Windows implementation |
| `macos-icon-frame` | Unsupported | No exposed Windows implementation |
| `macos-icon-ghost-color` | Unsupported | No exposed Windows implementation |
| `macos-icon-screen-color` | Unsupported | No exposed Windows implementation |
| `macos-shortcuts` | Unsupported | No exposed Windows implementation |
| `linux-cgroup` | Unsupported | No exposed Windows implementation |
| `linux-cgroup-memory-limit` | Unsupported | No exposed Windows implementation |
| `linux-cgroup-processes-limit` | Unsupported | No exposed Windows implementation |
| `linux-cgroup-hard-fail` | Unsupported | No exposed Windows implementation |
| `gtk-opengl-debug` | Unsupported | No exposed Windows implementation |
| `gtk-single-instance` | Unsupported | No exposed Windows implementation |
| `gtk-titlebar` | Unsupported | No exposed Windows implementation |
| `gtk-tabs-location` | Unsupported | No exposed Windows implementation |
| `gtk-tabs-inverted` | Unsupported | No exposed Windows implementation |
| `gtk-titlebar-hide-when-maximized` | Unsupported | No exposed Windows implementation |
| `gtk-toolbar-style` | Unsupported | No exposed Windows implementation |
| `gtk-titlebar-style` | Unsupported | No exposed Windows implementation |
| `gtk-wide-tabs` | Unsupported | No exposed Windows implementation |
| `gtk-horizontal-tab-scroll` | Unsupported | No exposed Windows implementation |
| `gtk-custom-css` | Unsupported | No exposed Windows implementation |
| `desktop-notifications` | Unsupported | No exposed Windows implementation |
| `progress-style` | Unsupported | No exposed Windows implementation |
| `bold-color` | Unsupported | No exposed Windows implementation |
| `faint-opacity` | Unsupported | No exposed Windows implementation |
| `term` | Unsupported | No exposed Windows implementation |
| `enquiry-response` | Unsupported | No exposed Windows implementation |
| `async-backend` | Unsupported | No exposed Windows implementation |
| `auto-update` | Unsupported | No exposed Windows implementation |
| `auto-update-channel` | Unsupported | No exposed Windows implementation |
| `_arena` | Unsupported | No exposed Windows implementation |
| `_diagnostics` | Unsupported | No exposed Windows implementation |
| `_conditional_state` | Unsupported | No exposed Windows implementation |
| `_conditional_set` | Unsupported | No exposed Windows implementation |
| `_replay_steps` | Unsupported | No exposed Windows implementation |
| `_xdg-terminal-exec` | Unsupported | No exposed Windows implementation |
