using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GhosttyWindows;

sealed record LaunchOptions(string? Profile, string? Directory, string? Command, string? Title, double? Width, double? Height, bool Maximize, bool Fullscreen)
{
    internal static LaunchOptions Parse(string[] args)
    {
        string? profile = null, directory = null, command = null, title = null;
        double? width = null, height = null; bool maximize = false, fullscreen = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string option = args[i]; if (!seen.Add(option)) throw new ArgumentException("Repeated option: " + option);
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing value for " + option);
            switch (option)
            {
                case "--profile": profile = Value(); if (!Settings.Profiles.ContainsKey(profile)) throw new ArgumentException("Unknown profile: " + profile); break;
                case "--directory": directory = Path.GetFullPath(Value()); if (!System.IO.Directory.Exists(directory)) throw new ArgumentException("Startup directory does not exist: " + directory); break;
                case "--command": command = Value(); if (string.IsNullOrWhiteSpace(command) || command.Contains('\0')) throw new ArgumentException("Command must be a Windows process command line."); break;
                case "--title": title = Value(); break;
                case "--width": width = double.Parse(Value(), CultureInfo.InvariantCulture); if (!double.IsFinite(width.Value) || width < 540 || width > 10000) throw new ArgumentException("Width must be 540–10000 DIPs."); break;
                case "--height": height = double.Parse(Value(), CultureInfo.InvariantCulture); if (!double.IsFinite(height.Value) || height < 320 || height > 10000) throw new ArgumentException("Height must be 320–10000 DIPs."); break;
                case "--maximize": maximize = true; break;
                case "--fullscreen": fullscreen = true; break;
                default: throw new ArgumentException("Unknown argument: " + option + ". Use --help for launch options.");
            }
        }
        return new(profile, directory, command, title, width, height, maximize, fullscreen);
    }
    internal static void Start(string[] args)
    {
        if (args.Length == 1 && args[0] == "--help") { Dialogs.Show(Help, "Ghostty Windows command line"); Application.Current.Shutdown(); return; }
        if (args.Length == 1 && args[0] == "--diagnostics")
        {
            Dialogs.Show(JsonSerializer.Serialize(new { version = typeof(App).Assembly.GetName().Version?.ToString(), os = Environment.OSVersion.ToString(), architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), settings = Settings.FilePath, runtime = Environment.Version.ToString() }, new JsonSerializerOptions { WriteIndented = true }), "Ghostty Windows diagnostics");
            Application.Current.Shutdown(); return;
        }
        if (args.Length == 2 && args[0] == "--diagnostics-file") { DiagnosticReport.Write(args[1]); Application.Current.Shutdown(); return; }
        var launch = Parse(args); var settings = Settings.Load();
        // Explicit launch requests open one fresh tab instead of inheriting a saved workspace.
        new MainWindow(settings, restore: args.Length == 0 ? settings.Workspace : null, launch: args.Length == 0 ? null : launch).Show();
    }
    internal const string Help = "Ghostty.Windows.exe [options]\n\n--profile \"PowerShell|Command Prompt|WSL\"\n--directory PATH\n--command \"Windows executable and arguments\"\n--title TEXT\n--width DIPS --height DIPS\n--maximize\n--fullscreen\n\n--help: show this reference\n--diagnostics: show version/platform/settings path\n--diagnostics-file PATH: export artifact/platform JSON without opening a shell\n\nLaunch overrides are not saved as preferences. Explicit launch options open one fresh tab. --command is passed directly to CreateProcess; use cmd.exe /c, PowerShell -Command or wsl.exe explicitly for shell syntax.";
}
