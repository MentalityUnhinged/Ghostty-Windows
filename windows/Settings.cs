using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace GhosttyWindows;

sealed partial class Settings
{
    string storagePath = FilePath;
    public string FontFamily { get; set; } = DefaultRegularFont();
    public double FontSize { get; set; } = 15;
    public string BackgroundEffect { get; set; } = "Solid";
    public double BackgroundOpacity { get; set; } = 85;
    public string Theme { get; set; } = "Midnight";
    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string DefaultProfile { get; set; } = "PowerShell";
    public bool ConfirmClose { get; set; } = true;
    public bool RestoreTabs { get; set; } = true;
    public bool CopyOnSelect { get; set; }
    public bool ConfirmMultilinePaste { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public bool ShowWelcome { get; set; } = true;
    public bool AnimateWelcome { get; set; }
    public bool FocusFollowsMouse { get; set; }
    public int WheelScrollLines { get; set; } = 2;
    public bool HideMouseWhileTyping { get; set; }
    public bool ClearSelectionOnCopy { get; set; }
    public bool ClearSelectionOnTyping { get; set; } = true;
    public string RightClickAction { get; set; } = "context-menu";
    public string MiddleClickAction { get; set; } = "paste";
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ulong? ScrollbackLimitBytes { get; set; } = 64UL*1024*1024;
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public ulong? ImageLimitBytes { get; set; } = 64UL*1024*1024;
    internal sealed record MouseAction(string Id, string Label);
    internal static readonly IReadOnlyList<MouseAction> MouseActions = Array.AsReadOnly(new[] { new MouseAction("context-menu","Context menu"), new("paste","Paste"), new("copy","Copy"), new("copy-or-paste","Copy selection, otherwise paste"), new("none","Do nothing"), new("primary-paste","Paste primary selection") });
    static readonly PropertyInfo[] DataProperties = typeof(Settings).GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true).ToArray();
    internal string StoragePath => storagePath;
    public event Action? Changed;
    double? fontResetSize;
    public void SetRuntimeFontSize(double size)
    {
        if (!double.IsFinite(size) || size < 8 || size > 48) throw new ArgumentOutOfRangeException(nameof(size));
        fontResetSize ??= FontSize; FontSize = size; Changed?.Invoke();
    }
    public void ResetFontSize() => SetRuntimeFontSize(fontResetSize ?? FontSize);
    public Workspace? Workspace { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ghostty.Windows");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static Settings Load() => LoadFile(FilePath);
    internal static Settings LoadFile(string path) => LoadJson(File.Exists(path) ? File.ReadAllText(path) : null, path);
    internal static Settings LoadJson(string? json, string path)
    {
        Settings s = json == null ? new() : JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new();
        s.storagePath = Path.GetFullPath(path);
        if (!Directory.Exists(s.WorkingDirectory)) s.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // Migrate the previous flat tab representation once at its owning boundary.
        var legacyKey = s.Extra?.Keys.FirstOrDefault(k => k.Equals("Tabs", StringComparison.OrdinalIgnoreCase));
        if (legacyKey != null)
        {
            var legacy = s.Extra![legacyKey];
            if (legacy.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null)) throw new InvalidDataException("Legacy tab profiles must be an array");
            if (s.Workspace == null && legacy.ValueKind == JsonValueKind.Array)
            {
                var profiles = legacy.EnumerateArray().Select(p =>
                {
                    if (p.ValueKind != JsonValueKind.String || p.GetString() is not string profile || !Profiles.ContainsKey(profile)) throw new InvalidDataException("Unknown legacy shell profile; settings were not modified");
                    return profile;
                }).ToArray();
                if (profiles.Length > 0) s.Workspace = new Workspace { Tabs = profiles.Select(p => new WorkspaceTab { Root = PaneLayout.Pane(p, s.WorkingDirectory) }).ToArray() };
            }
            s.Extra.Remove(legacyKey);
        }
        s.Validate();
        return s;
    }
    public void Save() => SaveFile(storagePath);
    public Settings Edit() { var draft = (Settings)MemberwiseClone(); draft.Changed = null; draft.watcher = null; draft.watcherUsers = 0; draft.Extra = Extra == null ? null : new Dictionary<string, JsonElement>(Extra); draft.Workspace = Workspace?.Copy(); draft.KeyBindings = KeyBindings.ToList(); return draft; }
    public void Commit(Settings draft)
    {
        // The existing settings owner chooses the destination. Persist first:
        // failed writes must not partially apply live appearance or behavior.
        draft.SaveFile(storagePath);
        Apply(draft);
    }
    internal void ApplyExternal(Settings candidate) => Apply(candidate);
    void Apply(Settings candidate) { foreach (var property in DataProperties) property.SetValue(this,property.GetValue(candidate)); AdoptBindings(candidate); fontResetSize = candidate.FontSize; Changed?.Invoke(); }
    internal void Reload() => Apply(LoadFile(storagePath));
    internal void ImportPreferences(string path, Workspace workspace)
    {
        var candidate = LoadFile(path); candidate.Workspace = workspace;
        var extra = Extra == null ? new Dictionary<string,JsonElement>() : new Dictionary<string,JsonElement>(Extra);
        if(candidate.Extra != null)foreach(var pair in candidate.Extra)extra[pair.Key]=pair.Value;
        candidate.Extra = extra.Count == 0 ? null : extra; Commit(candidate);
    }
    internal void ResetPreferences(Workspace workspace) => Commit(new Settings { Workspace = workspace, Extra = Extra });
    internal void ExportPreferences(string path) { var candidate=Edit();candidate.Workspace=null;candidate.SaveFile(path); }
    internal void Validate()
    {
        if (BackgroundEffect is not ("Solid" or "Acrylic")) throw new ArgumentException("Unknown background effect");
        if (!double.IsFinite(BackgroundOpacity) || BackgroundOpacity < 0 || BackgroundOpacity > 100) throw new ArgumentException("Background opacity must be between 0 and 100");
        ValidateBindings();
        ValidatePartialPreferences();
        ValidateAppearance();
        Workspace?.Validate();
        if(WheelScrollLines is < 0 or > 100)throw new InvalidDataException("Wheel scroll lines must be between 0 and 100.");
        if(!MouseActions.Any(a=>a.Id==RightClickAction)||!MouseActions.Any(a=>a.Id==MiddleClickAction))throw new InvalidDataException("Unknown mouse click action.");
    }
    internal void SaveFile(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
            if (File.Exists(path)) File.Replace(temp, path, path + ".backup");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static Dictionary<string, string> Profiles => new()
    {
        ["PowerShell"] = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe") + "\" -NoLogo",
        ["Command Prompt"] = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe") + "\"",
        ["WSL"] = "\"" + Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe") + "\""
    };
    public (uint Fg, uint Bg) Colors => EffectiveTheme switch
    {
        "Paper" => (0x243244, 0xf4f1e9), "Dracula" => (0xf8f8f2, 0x282a36), "Nord" => (0xd8dee9, 0x2e3440), _ => (0xdce3ef, 0x11151e)
    };
}
