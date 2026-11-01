# Action support matrix

Generated from `src/input/Binding.zig` at engine base `d2865b8f578fa4e85e2f2b85ef97f5ef69de951f`.
Source SHA-256: `302bd56e2d0be65bd559ffc269feec7e06f650857a27dea25cab04b8051367b1`. Inventory: **88 declarations**.

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
| `ignore` | Partial: Windows equivalent | ignore |
| `unbind` | Partial: Windows equivalent | unbind |
| `csi` | Partial: Windows equivalent | send-csi |
| `esc` | Partial: Windows equivalent | send-escape |
| `text` | Partial: Windows equivalent | send-text |
| `cursor_key` | Partial: Windows equivalent | send-cursor |
| `reset` | Partial: Windows equivalent | reset-terminal |
| `copy_to_clipboard` | Partial: Windows equivalent | copy |
| `paste_from_clipboard` | Partial: Windows equivalent | paste |
| `paste_from_selection` | Partial: Windows equivalent | paste-primary-selection |
| `copy_url_to_clipboard` | Partial: Windows equivalent | copy-hovered-url |
| `copy_title_to_clipboard` | Partial: Windows equivalent | copy-title |
| `increase_font_size` | Partial: Windows equivalent | increase-font-size |
| `decrease_font_size` | Partial: Windows equivalent | decrease-font-size |
| `reset_font_size` | Partial: Windows equivalent | reset-font-size |
| `set_font_size` | Partial: Windows equivalent | set-font-size |
| `search` | Unsupported | No exposed Windows implementation |
| `search_selection` | Partial: Windows equivalent | search-selection |
| `navigate_search` | Unsupported | No exposed Windows implementation |
| `start_search` | Partial: Windows equivalent | find |
| `end_search` | Partial: Windows equivalent | close-search |
| `clear_screen` | Partial: Windows equivalent | clear-screen |
| `select_all` | Partial: Windows equivalent | select-all |
| `scroll_to_top` | Partial: Windows equivalent | scroll-top |
| `scroll_to_bottom` | Partial: Windows equivalent | scroll-bottom |
| `scroll_to_selection` | Partial: Windows equivalent | scroll-selection |
| `scroll_to_row` | Partial: Windows equivalent | scroll-row |
| `scroll_page_up` | Partial: Windows equivalent | scroll-page-up |
| `scroll_page_down` | Partial: Windows equivalent | scroll-page-down |
| `scroll_page_fractional` | Unsupported | No exposed Windows implementation |
| `scroll_page_lines` | Unsupported | No exposed Windows implementation |
| `adjust_selection` | Unsupported | No exposed Windows implementation |
| `jump_to_prompt` | Unsupported | No exposed Windows implementation |
| `write_scrollback_file` | Partial: Windows equivalent | export-scrollback / export-html / export-scrollback-ansi |
| `write_screen_file` | Partial: Windows equivalent | export-screen / export-screen-html / export-screen-ansi |
| `write_selection_file` | Partial: Windows equivalent | export-selection / export-selection-html / export-selection-ansi |
| `new_window` | Partial: Windows equivalent | new-window |
| `new_tab` | Partial: Windows equivalent | new-tab-default |
| `previous_tab` | Partial: Windows equivalent | previous-tab |
| `next_tab` | Partial: Windows equivalent | next-tab |
| `last_tab` | Unsupported | No exposed Windows implementation |
| `goto_tab` | Unsupported | No exposed Windows implementation |
| `move_tab` | Unsupported | No exposed Windows implementation |
| `move_tab_to_new_window` | Unsupported | No exposed Windows implementation |
| `toggle_tab_overview` | Unsupported | No exposed Windows implementation |
| `prompt_surface_title` | Unsupported | No exposed Windows implementation |
| `prompt_tab_title` | Unsupported | No exposed Windows implementation |
| `prompt_window_title` | Unsupported | No exposed Windows implementation |
| `set_surface_title` | Unsupported | No exposed Windows implementation |
| `set_tab_title` | Unsupported | No exposed Windows implementation |
| `set_window_title` | Unsupported | No exposed Windows implementation |
| `new_split` | Unsupported | No exposed Windows implementation |
| `goto_split` | Unsupported | No exposed Windows implementation |
| `goto_window` | Unsupported | No exposed Windows implementation |
| `toggle_split_zoom` | Unsupported | No exposed Windows implementation |
| `toggle_readonly` | Unsupported | No exposed Windows implementation |
| `resize_split` | Unsupported | No exposed Windows implementation |
| `equalize_splits` | Unsupported | No exposed Windows implementation |
| `reset_window_size` | Unsupported | No exposed Windows implementation |
| `inspector` | Partial: Windows equivalent | terminal-inspector |
| `show_gtk_inspector` | Unsupported | No exposed Windows implementation |
| `show_on_screen_keyboard` | Unsupported | No exposed Windows implementation |
| `open_config` | Partial: Windows equivalent | open-config |
| `reload_config` | Partial: Windows equivalent | reload-settings |
| `close_surface` | Unsupported | No exposed Windows implementation |
| `close_tab` | Partial: Windows equivalent | close-tab |
| `close_window` | Partial: Windows equivalent | close-window |
| `close_all_windows` | Unsupported | No exposed Windows implementation |
| `toggle_maximize` | Unsupported | No exposed Windows implementation |
| `toggle_fullscreen` | Partial: Windows equivalent | fullscreen |
| `toggle_window_decorations` | Unsupported | No exposed Windows implementation |
| `toggle_window_float_on_top` | Unsupported | No exposed Windows implementation |
| `toggle_secure_input` | Unsupported | No exposed Windows implementation |
| `toggle_mouse_reporting` | Unsupported | No exposed Windows implementation |
| `toggle_command_palette` | Partial: Windows equivalent | command-palette |
| `toggle_quick_terminal` | Unsupported | No exposed Windows implementation |
| `toggle_visibility` | Unsupported | No exposed Windows implementation |
| `toggle_background_opacity` | Unsupported | No exposed Windows implementation |
| `check_for_updates` | Unsupported | No exposed Windows implementation |
| `undo` | Unsupported | No exposed Windows implementation |
| `redo` | Unsupported | No exposed Windows implementation |
| `end_key_sequence` | Unsupported | No exposed Windows implementation |
| `activate_key_table` | Unsupported | No exposed Windows implementation |
| `activate_key_table_once` | Unsupported | No exposed Windows implementation |
| `deactivate_key_table` | Unsupported | No exposed Windows implementation |
| `deactivate_all_key_tables` | Unsupported | No exposed Windows implementation |
| `quit` | Partial: Windows equivalent | Close all windows manually (no process-wide action) |
| `crash` | Unsupported | No exposed Windows implementation |
