using System.Text;
using System.Windows.Input;

namespace GhosttyWindows;

sealed record CustomBinding
{
    public string Chord { get; init; } = "";
    public string Action { get; init; } = "";
    public string? Value { get; init; }
    internal byte[] Encode(Engine engine) => Action switch
    {
        "send-text" => Encoding.UTF8.GetBytes(Value!),
        "send-csi" => Encoding.UTF8.GetBytes("\x1b[" + Value),
        "send-escape" => Encoding.UTF8.GetBytes("\x1b" + Value),
        "send-cursor" => engine.Key(Value switch { "Left" => 0x25, "Up" => 0x26, "Right" => 0x27, "Down" => 0x28, "Home" => 0x24, "End" => 0x23, _ => throw new ArgumentException("Invalid cursor action") }, 0, 1),
        _ => throw new ArgumentException("Not a terminal send action")
    };
}

sealed partial class Settings
{
    public List<CustomBinding> KeyBindings { get; set; } = new();
    public string ShortcutPrecedence { get; set; } = "Application";
    internal (KeyChord Chord, CustomBinding Binding)[] CompiledBindings { get; private set; } = Array.Empty<(KeyChord, CustomBinding)>();
    internal void AdoptBindings(Settings candidate) => CompiledBindings = candidate.CompiledBindings;
    internal void ValidateBindings()
    {
        if (KeyBindings == null || ShortcutPrecedence is not ("Application" or "Terminal")) throw new ArgumentException("Invalid keybinding preferences.");
        var chords = new HashSet<KeyChord>();
        var compiled = new List<(KeyChord, CustomBinding)>();
        foreach (var binding in KeyBindings)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Action)) throw new ArgumentException("Each keybinding needs a chord and action.");
            var chord = KeyChord.Parse(binding.Chord);
            if (!chords.Add(chord)) throw new ArgumentException("Repeated key chord: " + binding.Chord);
            if (chord == new KeyChord(Key.OemComma, ModifierKeys.Control) || chord == new KeyChord(Key.P, ModifierKeys.Control | ModifierKeys.Shift)) throw new ArgumentException("Ctrl+, and Ctrl+Shift+P remain available for Settings and command recovery.");
            compiled.Add((chord, binding));
            if (binding.Action.StartsWith("send-", StringComparison.Ordinal) && binding.Action is not ("send-text" or "send-csi" or "send-escape" or "send-cursor")) throw new ArgumentException("Unknown send action: " + binding.Action);
            if (binding.Action is "send-text" or "send-csi" or "send-escape" or "send-cursor" && binding.Value == null) throw new ArgumentException("Send actions require Value.");
            if (binding.Action == "send-cursor" && !new[] { "Left", "Right", "Up", "Down", "Home", "End" }.Contains(binding.Value)) throw new ArgumentException("send-cursor Value must be Left/Right/Up/Down/Home/End.");
        }
        CompiledBindings = compiled.ToArray();
    }
}

sealed partial class MainWindow
{
    Dictionary<string, (KeyChord? Primary, IReadOnlyList<KeyChord> Alternate)>? shortcutDefaults;
    void RefreshShortcutLabels()
    {
        shortcutDefaults ??= Commands.ToDictionary(c => c.Id, c => (c.Gesture, c.AlternateGestures));
        foreach (var command in Commands)
        {
            var original = shortcutDefaults[command.Id];
            var chords = new[] { original.Primary }.Concat(original.Alternate).OfType<KeyChord>().Where(chord => (settings.ShortcutPrecedence == "Application" || command.Id is "settings" or "command-palette") && !settings.CompiledBindings.Any(b => b.Chord == chord)).ToList();
            chords.AddRange(settings.CompiledBindings.Where(b => b.Binding.Action == command.Id).Select(b => b.Chord));
            command.Gesture = chords.FirstOrDefault(); command.AlternateGestures = chords.Skip(1).ToArray(); command.Refresh();
        }
    }
    KeyEventArgs? terminalBypass;
    internal bool UseApplicationShortcuts(KeyEventArgs input) => settings.ShortcutPrecedence == "Application" && !ReferenceEquals(input, terminalBypass);
    internal bool DispatchCustom(KeyChord chord)
    {
        var binding = settings.CompiledBindings.FirstOrDefault(b => b.Chord == chord).Binding;
        if (binding == null) return false;
        if (binding.Action == "unbind") return false;
        if (binding.Action == "ignore") return true;
        if (binding.Action.StartsWith("send-", StringComparison.Ordinal))
        {
            if (active == null || active.ReadOnly) return true;
            byte[] bytes = binding.Encode(active.Engine);
            active.SendAction(bytes); return true;
        }
        if (Commands.Any(c => c.Id == binding.Action)) ExecuteCommand(binding.Action);
        else CapacityNotice("Unknown keybinding action: " + binding.Action + ". Inspect available command IDs in the terminal inspector.");
        return true;
    }
    bool HandleCustom(KeyEventArgs input)
    {
        terminalBypass = null;
        if (active?.IsKeyboardFocusWithin != true || input.Key == Key.ImeProcessed) return false;
        var binding = settings.CompiledBindings.FirstOrDefault(b => b.Chord.Matches(input)).Binding;
        if (binding == null) return false;
        if (binding.Action == "unbind") { terminalBypass = input; return true; }
        input.Handled = DispatchCustom(settings.CompiledBindings.Single(b => ReferenceEquals(b.Binding, binding)).Chord); return true;
    }
}
