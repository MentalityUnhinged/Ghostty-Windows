using System.Windows.Input;

namespace GhosttyWindows;

// Terminal chords include Shift+Insert/PageUp, which WPF KeyGesture rejects.
// This owner supplies both matching and the palette label for the same chord.
sealed record KeyChord(Key Key, ModifierKeys Modifiers = ModifierKeys.None)
{
    internal static KeyChord Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Key chord cannot be empty.");
        string[] parts = text.Split('+', StringSplitOptions.TrimEntries); ModifierKeys modifiers = ModifierKeys.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            ModifierKeys modifier = parts[i].ToLowerInvariant() switch { "ctrl" or "control" => ModifierKeys.Control, "alt" => ModifierKeys.Alt, "shift" => ModifierKeys.Shift, "win" or "windows" => ModifierKeys.Windows, _ => throw new ArgumentException("Unknown key modifier: " + parts[i]) };
            if (modifiers.HasFlag(modifier)) throw new ArgumentException("Repeated key modifier: " + parts[i]); modifiers |= modifier;
        }
        string name = parts[^1];
        Key key = name.ToLowerInvariant() switch { "plus" => System.Windows.Input.Key.OemPlus, "minus" => System.Windows.Input.Key.OemMinus, "comma" or "," => System.Windows.Input.Key.OemComma, _ => name.Length == 1 && char.IsAsciiDigit(name[0]) ? System.Windows.Input.Key.D0 + (name[0] - '0') : Enum.TryParse<Key>(name, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw new ArgumentException("Unknown key: " + name) };
        if (key is System.Windows.Input.Key.None or System.Windows.Input.Key.System or System.Windows.Input.Key.ImeProcessed) throw new ArgumentException("Key cannot be used in a binding.");
        return new(key, modifiers);
    }
    internal bool Matches(KeyEventArgs input)
    {
        if (input.Key == System.Windows.Input.Key.ImeProcessed) return false;
        Key actual = input.Key == System.Windows.Input.Key.System ? input.SystemKey : input.Key;
        return actual == Key && Keyboard.Modifiers == Modifiers;
    }
    internal string Display
    {
        get
        {
            var parts = new List<string>();
            foreach (var (flag, label) in new[] { (ModifierKeys.Control, "Ctrl"), (ModifierKeys.Alt, "Alt"), (ModifierKeys.Shift, "Shift"), (ModifierKeys.Windows, "Win") }) if (Modifiers.HasFlag(flag)) parts.Add(label);
            string key = Key switch { System.Windows.Input.Key.OemComma => ",", System.Windows.Input.Key.OemPlus => "Plus", System.Windows.Input.Key.OemMinus => "Minus", _ => Key >= System.Windows.Input.Key.D0 && Key <= System.Windows.Input.Key.D9 ? ((int)(Key - System.Windows.Input.Key.D0)).ToString() : Key.ToString() };
            parts.Add(key); return string.Join("+", parts);
        }
    }
}
