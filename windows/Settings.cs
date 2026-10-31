using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhosttyWindows;

sealed class Settings
{
    string storagePath = FilePath;
    public string FontFamily { get; set; } = "Cascadia Mono";
    public double FontSize { get; set; } = 15;
    public string Theme { get; set; } = "Midnight";
    public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string DefaultProfile { get; set; } = "PowerShell";
    public bool ConfirmClose { get; set; } = true;
    public bool RestoreTabs { get; set; } = true;
    public bool CopyOnSelect { get; set; }
    public bool ConfirmMultilinePaste { get; set; } = true;
    public bool ShowStatusBar { get; set; } = true;
    public Workspace? Workspace { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ghostty.Windows");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static Settings Load() => LoadFile(FilePath);
    internal static Settings LoadFile(string path)
    {
        Settings s = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions) ?? new() : new();
        s.storagePath = Path.GetFullPath(path);
        if (!double.IsFinite(s.FontSize) || s.FontSize < 8 || s.FontSize > 48) s.FontSize = 15;
        if (string.IsNullOrWhiteSpace(s.FontFamily)) s.FontFamily = "Cascadia Mono";
        if (!Directory.Exists(s.WorkingDirectory)) s.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (s.DefaultProfile == null || !Profiles.ContainsKey(s.DefaultProfile)) s.DefaultProfile = "PowerShell";
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
        s.Workspace?.Validate();
        return s;
    }
    public void Save() => SaveFile(storagePath);
    public Settings Edit() => (Settings)MemberwiseClone();
    public void Commit(Settings draft)
    {
        // The existing settings owner chooses the destination. Persist first:
        // failed writes must not partially apply live appearance or behavior.
        draft.SaveFile(storagePath);
        FontFamily = draft.FontFamily; FontSize = draft.FontSize; Theme = draft.Theme;
        WorkingDirectory = draft.WorkingDirectory; DefaultProfile = draft.DefaultProfile;
        ConfirmClose = draft.ConfirmClose; RestoreTabs = draft.RestoreTabs;
        CopyOnSelect = draft.CopyOnSelect; ConfirmMultilinePaste = draft.ConfirmMultilinePaste;
        ShowStatusBar = draft.ShowStatusBar; Workspace = draft.Workspace;
    }
    internal void SaveFile(string path)
    {
        Workspace?.Validate();
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
    public (uint Fg, uint Bg) Colors => Theme switch
    {
        "Paper" => (0x243244, 0xf4f1e9), "Dracula" => (0xf8f8f2, 0x282a36), "Nord" => (0xd8dee9, 0x2e3440), _ => (0xdce3ef, 0x11151e)
    };
}
