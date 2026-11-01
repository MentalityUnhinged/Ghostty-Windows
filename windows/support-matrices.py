"""Inventory pinned Zig declarations without treating comments/literals as syntax.
Run from any directory; --check verifies generated documents without writing.
"""
from pathlib import Path
import hashlib
import re
import sys

ROOT = Path(__file__).resolve().parent.parent

def declarations(relative, action=False):
    source = (ROOT / relative).read_text(encoding="utf-8")
    if action:
        start = source.index("pub const Action = union(enum) {")
        end = source.index("    pub const Key = @typeInfo(Action)", start)
        source = source[start:end]
    # Keep positions/newlines, masking lexical contents before counting braces.
    lexical = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|\\\\[^\n]*|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    masked = lexical.sub(lambda m: "".join("\n" if c == "\n" else " " for c in m[0]), source)
    depth, fields = 0, []
    field = re.compile((r'^    ' if action else r'^') + r'(?:@"([^"]+)"|([A-Za-z_][A-Za-z_0-9]*))\s*([:,])')
    for original, cleaned in zip(source.splitlines(), masked.splitlines()):
        match = field.match(original)
        if match and depth == (1 if action else 0) and not cleaned.lstrip().startswith("//"):
            fields.append((match[1] or match[2], match[3] == ":"))
        depth += cleaned.count("{") - cleaned.count("}")
    if len(fields) != len(set(name for name, _ in fields)):
        raise ValueError("Duplicate declarations in " + relative)
    if len(fields) < (80 if action else 100):
        raise ValueError("Unexpected source structure: " + relative)
    return fields

CONFIG = {
    "font-family": "FontFamily", "font-family-bold": "BoldFontFamily", "font-family-italic": "ItalicFontFamily", "font-family-bold-italic": "BoldItalicFontFamily", "font-size": "FontSize", "theme": "Theme / AutoTerminalTheme", "background-opacity": "BackgroundOpacity / BackgroundEffect", "background-image": "BackgroundImagePath", "background-image-opacity": "BackgroundImageOpacity", "background-image-fit": "BackgroundImageFit", "background-image-position": "BackgroundImageAlignment / BackgroundImageVerticalAlignment", "background-image-repeat": "BackgroundImageRepeat", "cursor-style": "CursorShape", "cursor-color": "CursorColor", "cursor-text": "CursorTextColor", "cursor-opacity": "CursorOpacity", "cursor-style-blink": "CursorBlink / FollowApplicationCursorBlink", "selection-background": "SelectionBackground", "window-padding-x": "PaddingHorizontal", "window-padding-y": "PaddingVertical", "window-padding-balance": "BalancePadding", "unfocused-split-opacity": "InactivePaneOpacity", "minimum-contrast": "MinimumTextContrast", "working-directory": "WorkingDirectory", "confirm-close-surface": "ConfirmClose", "keybind": "KeyBindings / ShortcutPrecedence", "scrollback-limit-bytes": "ScrollbackLimitBytes", "image-storage-limit": "ImageLimitBytes", "window-decoration": "decorations command (not a persisted preference)", "window-titlebar-background": "OS dark/light title bar policy", "mouse-hide-while-typing": "HideMouseWhileTyping", "mouse-shift-capture": "ShiftMouseOverride", "click-repeat-interval": "RepeatClickMilliseconds", "copy-on-select": "CopyOnSelect", "clipboard-trim-trailing-spaces": "CopyTrimTrailing", "clipboard-paste-protection": "PasteControlPolicy / confirmation policy", "window-save-state": "Workspace", "window-inherit-working-directory": "new-tab-folder command (explicit action only)", "quit-after-last-window-closed": "Current lifetime policy (always quit)",
}
ACTION = {
    "ignore": "ignore", "unbind": "unbind", "text": "send-text", "csi": "send-csi", "esc": "send-escape", "cursor_key": "send-cursor", "reset": "reset-terminal", "copy_to_clipboard": "copy", "paste_from_clipboard": "paste", "paste_from_selection": "paste-primary-selection", "copy_url_to_clipboard": "copy-hovered-url", "copy_title_to_clipboard": "copy-title", "increase_font_size": "increase-font-size", "decrease_font_size": "decrease-font-size", "reset_font_size": "reset-font-size", "set_font_size": "set-font-size", "search_selection": "search-selection", "start_search": "find", "end_search": "close-search", "clear_screen": "clear-screen", "select_all": "select-all", "scroll_to_top": "scroll-top", "scroll_to_bottom": "scroll-bottom", "scroll_to_selection": "scroll-selection", "scroll_to_row": "scroll-row", "scroll_page_up": "scroll-page-up", "scroll_page_down": "scroll-page-down", "new_window": "new-window", "new_tab": "new-tab-default", "previous_tab": "previous-tab", "next_tab": "next-tab", "close_tab": "close-tab", "close_window": "close-window", "toggle_fullscreen": "fullscreen", "toggle_command_palette": "command-palette", "open_config": "open-config", "reload_config": "reload-settings", "quit": "Close all windows manually (no process-wide action)", "inspector": "terminal-inspector", "write_screen_file": "export-screen / export-screen-html / export-screen-ansi", "write_scrollback_file": "export-scrollback / export-html / export-scrollback-ansi", "write_selection_file": "export-selection / export-selection-html / export-selection-ansi",
}

def generate(relative, mapping, action=False):
    fields = declarations(relative, action)
    unknown = set(mapping) - {name for name, _ in fields}
    # Stale mappings must not silently advertise nonexistent upstream options.
    if unknown:
        raise ValueError("Unknown upstream names: " + ", ".join(sorted(unknown)))
    digest = hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()
    rows = []
    for name, payload in fields:
        equivalent = mapping.get(name)
        status = "Partial: Windows equivalent" if equivalent else "Unsupported"
        rows.append(f"| `{name}` | {status} | {equivalent or 'No exposed Windows implementation'} |")
    return f"""# {'Action' if action else 'Configuration option'} support matrix

Generated from `{relative}` at engine base `{(ROOT / 'windows/engine-base.txt').read_text().strip()}`.
Source SHA-256: `{digest}`. Inventory: **{len(fields)} declarations**.

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
""" + "\n".join(rows) + "\n"

def main():
    for filename, relative, mapping, action in [("CONFIG_SUPPORT.md", "src/config/Config.zig", CONFIG, False), ("ACTION_SUPPORT.md", "src/input/Binding.zig", ACTION, True)]:
        content = generate(relative, mapping, action)
        destination = ROOT / "windows" / filename
        if "--check" in sys.argv:
            if not destination.exists() or destination.read_text(encoding="utf-8") != content:
                raise SystemExit("Stale matrix: " + filename)
        else:
            destination.write_text(content, encoding="utf-8")
        print(filename + ": verified")

if __name__ == "__main__":
    main()
