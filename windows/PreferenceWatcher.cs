using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace GhosttyWindows;

// One watcher per settings owner, shared by its windows; callbacks stay on its UI dispatcher.
sealed class PreferenceWatcher : IDisposable
{
    readonly Settings settings;
    readonly FileSystemWatcher watcher;
    readonly Dispatcher dispatcher;
    readonly DispatcherTimer debounce;
    string? lastText;
    bool disposed;
    internal event Action<string>? StatusChanged;
    internal PreferenceWatcher(Settings settings)
    {
        this.settings = settings; dispatcher = Dispatcher.CurrentDispatcher;
        string path = settings.StoragePath; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher); debounce.Tick += (_, _) => { debounce.Stop(); Reload(); };
        watcher.Changed += Change; watcher.Created += Change; watcher.Renamed += Rename; watcher.Error += Error;
        settings.Changed += Configure; Configure();
    }
    void Change(object sender, FileSystemEventArgs args) => dispatcher.BeginInvoke((Action)(() => { if (disposed || !settings.AutoReloadSettings) return; debounce.Stop(); debounce.Interval = TimeSpan.FromMilliseconds(settings.ReloadDebounceMilliseconds); debounce.Start(); }));
    void Error(object sender, ErrorEventArgs args) => dispatcher.BeginInvoke((Action)(() => { if (!disposed) StatusChanged?.Invoke("Settings watcher failed: " + args.GetException().Message); }));
    void Rename(object sender, RenamedEventArgs args) => Change(sender, args);
    void Configure() { if (disposed) return; watcher.EnableRaisingEvents = settings.AutoReloadSettings; }
    void Reload()
    {
        if (disposed || !settings.AutoReloadSettings) return;
        try
        {
            string text = File.ReadAllText(settings.StoragePath);
            if (text == lastText) return;
            // Validate before applying; malformed external edits leave live sessions/preferences intact.
            var candidate = Settings.LoadJson(text, settings.StoragePath); candidate.Workspace = settings.Workspace;
            settings.ApplyExternal(candidate); lastText = text; StatusChanged?.Invoke("");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException) { StatusChanged?.Invoke("Settings reload failed: " + e.Message); }
    }
    public void Dispose() { if (disposed) return; disposed = true; watcher.Changed -= Change; watcher.Created -= Change; watcher.Renamed -= Rename; watcher.Error -= Error; watcher.EnableRaisingEvents = false; watcher.Dispose(); debounce.Stop(); settings.Changed -= Configure; }
}
