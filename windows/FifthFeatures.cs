using System.IO;
using System.Windows;

namespace GhosttyWindows;

sealed partial class Settings
{
    public string BackgroundImagePath { get; set; } = "";
    public double BackgroundImageOpacity { get; set; } = .25;
    public string BackgroundImageFit { get; set; } = "Cover";
    public string BackgroundImageAlignment { get; set; } = "Center";
    public bool BackgroundImageRepeat { get; set; }
    public bool AutoReloadSettings { get; set; }
    public int ReloadDebounceMilliseconds { get; set; } = 500;
    PreferenceWatcher? watcher;
    int watcherUsers;
    internal IDisposable ObserveFile(Action<string> report)
    {
        watcher ??= new PreferenceWatcher(this); watcherUsers++;
        var owned = watcher; owned.StatusChanged += report;
        return new WatchLease(() => { owned.StatusChanged -= report; if (--watcherUsers == 0) { owned.Dispose(); watcher = null; } });
    }
    sealed class WatchLease(Action close) : IDisposable
    {
        Action? cleanup = close;
        public void Dispose() => Interlocked.Exchange(ref cleanup, null)?.Invoke();
    }
    void ValidateFifthPreferences()
    {
        if (BackgroundImagePath == null || BackgroundImageFit is not ("Cover" or "Contain" or "Stretch" or "Center") || BackgroundImageAlignment is not ("Left" or "Center" or "Right")) throw new ArgumentException("Invalid background image preference.");
        if (!double.IsFinite(BackgroundImageOpacity) || BackgroundImageOpacity < 0 || BackgroundImageOpacity > 1) throw new ArgumentException("Background image opacity must be between 0 and 1.");
        if (BackgroundImagePath.Length > 0 && (!Path.IsPathFullyQualified(BackgroundImagePath))) throw new ArgumentException("Choose an existing absolute background image path.");
        if (ReloadDebounceMilliseconds is < 100 or > 5000) throw new ArgumentException("Reload debounce must be 100–5000 milliseconds.");
    }
}

sealed partial class MainWindow
{
    void AddFifthBatch(List<WindowCommand> commands)
    {
        void Run(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException) { Dialogs.Show(this, e.Message, "Could not open Windows action"); }
        }
        commands.Add(new("open-working-directory", "Open working directory in Explorer", () => Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { ArgumentList = { active!.DirectoryForNewSession }, UseShellExecute = true })), () => active != null));
        commands.Add(new("copy-working-directory", "Copy working directory", () => TerminalView.CopyText(active!.DirectoryForNewSession), () => active != null));
        commands.Add(new("on-screen-keyboard", "Open Windows on-screen keyboard", () => Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "osk.exe")) { UseShellExecute = true })), () => true));
    }
}
