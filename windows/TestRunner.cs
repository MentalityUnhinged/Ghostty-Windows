using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;

namespace GhosttyWindows;

static class TestRunner
{
    sealed record Suite(string Scope, Func<string, Task> Run);
    static readonly IReadOnlyDictionary<string, Suite> suites = new Dictionary<string, Suite>(StringComparer.Ordinal)
    {
        ["--test-real-apps"] = new("real-applications-only", VerificationTests.Applications),
        ["--test-verification"] = new("verification-only", VerificationTests.Run),
        ["--test-missing"] = new("missing-completion-only", MissingCompletionTests.Run),
        ["--test-partials"] = new("partial-completion-only", PartialCompletionTests.Run),
        ["--self-test"] = new("full", Full),
        ["--test-fifth-batch"] = new("fifth-batch-only", FifthBatchTests.Run),
        ["--test-fourth-batch"] = new("fourth-batch-only", FourthBatchTests.Run),
        ["--test-third-batch"] = new("third-batch-only", ThirdBatchTests.Run),
        ["--test-wheel"] = new("wheel-only", _ => WheelScrollingTests.Run()),
        ["--test-welcome"] = new("animated-welcome-only", AnimatedWelcomeTests.Run)
    };
    internal static bool IsRequested(string[] args) => args.Length > 0 && suites.ContainsKey(args[0]);
    internal static async Task Run(string[] args, string artifacts)
    {
        if (!IsRequested(args)) throw new ArgumentException("Select a named test suite as the first argument.");
        bool hasArtifacts = false;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] != "--artifacts" || hasArtifacts || ++i >= args.Length) throw new ArgumentException("Test suites accept one optional --artifacts PATH argument.");
            hasArtifacts = true;
        }
        var suite = suites[args[0]];
        Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { ok = false, scope = suite.Scope, state = "running" }));
        await suite.Run(artifacts);
        if (suite.Scope != "full") File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { ok = true, scope = suite.Scope, tests = SelfTest.Passed, artifacts = ArtifactHashes() }));
    }
    static Dictionary<string,string> ArtifactHashes() => new[] { "Ghostty.Windows.exe", "Ghostty.Windows.dll", "ghostty-win.dll" }.ToDictionary(name => name, name => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name)))).ToLowerInvariant());
    static async Task Full(string artifacts)
    {
                    await SelfTest.Run(artifacts);
                    await GraphicsTests.Run(artifacts);
                    await WorkspaceTests.Run(artifacts);
                    await KeyboardTests.Run();
                    await SettingsTests.Run(artifacts);
                    await InteractionTests.Run();
                    await ChromeTests.Run(artifacts);
                    await WelcomeTests.Run(artifacts);
                    await FeatureBatchTests.Run(artifacts);
                    await SecondBatchTests.Run(artifacts);
                    await ThirdBatchTests.Run(artifacts);
                    await FourthBatchTests.Run(artifacts);
                    await WheelScrollingTests.Run();
                    await AnimatedWelcomeTests.Run(artifacts);
                    await FifthBatchTests.Run(artifacts);
                    await PartialCompletionTests.Run(artifacts);
                    await MissingCompletionTests.Run(artifacts);
                    SystemTitleBar.VerifyFollowingChanges(); SelfTest.Passed.Add("Native title bar follows dark/light changes without changing Windows preferences");
                    var window = new MainWindow(new Settings { ConfirmClose = false }, true);
                    window.Show(); await Task.Delay(300); window.ExerciseLayout(); SelfTest.Passed.Add("Nested split, pane close, tab creation and tab close"); await Task.Delay(800);
                    window.UpdateLayout();
                    if (window.Icon == null) throw new Exception("Window icon not loaded");
                    SelfTest.Passed.Add("Multi-resolution icon loads in WPF window");
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(artifacts, "preview.png"))) encoder.Save(file);
                    var hashes = ArtifactHashes();
                    File.WriteAllText(Path.Combine(artifacts, "result.json"), JsonSerializer.Serialize(new { ok = true, scope = "full", tests = SelfTest.Passed, screenshot = "preview.png", artifacts = hashes }, new JsonSerializerOptions { WriteIndented = true }));
                    window.Close();
    }
}
