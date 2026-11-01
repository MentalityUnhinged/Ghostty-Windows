using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GhosttyWindows;

// Bounded, repeatable tests of the compiled Windows engine/ConPTY boundary.
// Device, cross-machine and long-duration claims are deliberately excluded.
static class VerificationTests
{
    static void Check(bool value, string label) { if (!value) throw new Exception("Verification: " + label); SelfTest.Passed.Add(label); }
    static async Task Until(Func<bool> ready, int seconds = 20)
    { var clock = Stopwatch.StartNew(); while (!ready() && clock.Elapsed.TotalSeconds < seconds) await Task.Delay(25); if (!ready()) throw new TimeoutException("Verification boundary did not settle."); }
    static async Task RunProgram(string command, string marker, string artifacts, string name, string? input = null, string? ready = null)
    {
        using var engine = new Engine(); using var console = new PseudoConsole();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int sawMarker = 0; using var raw = new MemoryStream();
        console.Data += (bytes,count) => { lock(raw) { if(raw.Length < 262144) raw.Write(bytes,0,(int)Math.Min(count,262144-raw.Length)); } engine.Feed(bytes,count); if(engine.ScreenText().Contains(marker)) Interlocked.Exchange(ref sawMarker,1); }; engine.Reply += console.Send; console.Exited += code => exited.TrySetResult(code);
        try
        {
            console.Start(command, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 100, 30);
            if (input != null) { await Until(() => engine.ScreenText().Contains(ready ?? marker)); console.Send(Encoding.UTF8.GetBytes(input)); }
            int code = await exited.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Until(() => Volatile.Read(ref sawMarker) != 0 || engine.Text().Contains(marker));
            Check(code == 0, "Real application exits successfully through ConPTY: " + name);
            Check(Volatile.Read(ref sawMarker) != 0 || engine.Text().Contains(marker), "Real application output survives VT state transitions: " + name);
            Check(engine.Capture().Cells.Length == 3000, "Real application produces a readable 100x30 render frame: " + name);
        }
        finally
        {
            // Settle the owned session before diagnostic IO: a full/denied
            // artifact directory must not prevent child/pump cleanup.
            console.Dispose(); await console.Cleanup.WaitAsync(TimeSpan.FromSeconds(15));
            lock(raw) File.WriteAllBytes(Path.Combine(artifacts,name+".vt"),raw.ToArray());
            File.WriteAllText(Path.Combine(artifacts,name+".txt"),engine.Text());
        }
    }
    sealed record ProcessStats(int Handles, long PrivateBytes, double CpuMilliseconds, int ThreadPoolThreads);
    static ProcessStats ProcessSnapshot() { using var process = Process.GetCurrentProcess(); return new(process.HandleCount,process.PrivateMemorySize64,process.TotalProcessorTime.TotalMilliseconds,ThreadPool.ThreadCount); }
    public static async Task Run(string artifacts)
    {
        var metrics = new Dictionary<string, object>();
        // Retain the process HANDLE-backed Process instance: PID lookup after
        // teardown would not establish identity if Windows reused the PID.
        var beforeLifecycle = ProcessSnapshot(); var lifecycleSamples = new List<ProcessStats>();
        for (int i = 0; i < 20; i++)
        {
            using var console = new PseudoConsole();
            console.Start("cmd.exe /d /q /c \"echo lifecycle & ping -n 8 127.0.0.1 >nul\"", Path.GetTempPath(), 80, 24);
            using var child = Process.GetProcessById((int)console.ProcessId); _ = child.Handle;
            console.Dispose(); await console.Cleanup.WaitAsync(TimeSpan.FromSeconds(15));
            Check(child.HasExited && console.QueuedInputBytes == 0, "Close/reopen settles owned process and input pumps: " + i);
            if (i % 5 == 4) lifecycleSamples.Add(ProcessSnapshot());
        }
        for (int i = 0; i < 10; i++)
        {
            using var console = new PseudoConsole(); using var release = new ManualResetEventSlim();
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            console.Data += (_,_) => { entered.TrySetResult(true); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Reader test gate timed out"); };
            console.Exited += code => exited.TrySetResult(code);
            try
            {
                console.Start("cmd.exe /d /q /c echo CLOSE_READ_RACE",Path.GetTempPath(),80,24);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); await exited.Task.WaitAsync(TimeSpan.FromSeconds(10));
                console.Dispose(); release.Set(); await console.Cleanup.WaitAsync(TimeSpan.FromSeconds(15));
                Check(console.QueuedInputBytes == 0,"Natural exit settles an in-flight read callback before stream disposal: "+i);
            }
            finally { release.Set(); console.Dispose(); await console.Cleanup.WaitAsync(TimeSpan.FromSeconds(15)); }
        }
        for (int i = 0; i < 5; i++)
        {
            using var console = new PseudoConsole(); bool rejected = false;
            try { console.Start("Z:\\ghostty-verification-missing\\missing.exe", Path.GetTempPath(), 80,24); } catch (System.ComponentModel.Win32Exception) { rejected = true; }
            await console.Cleanup.WaitAsync(TimeSpan.FromSeconds(15)); Check(rejected && console.QueuedInputBytes == 0, "Failed launch releases partially initialized ConPTY: " + i);
        }
        metrics["lifecycle"] = new { iterations = 20, failedLaunches = 5, before = beforeLifecycle, samplesEveryFiveIterations = lifecycleSamples, after = ProcessSnapshot() };
        using (var engine = new Engine())
        {
            engine.Resize(80,24,9,20); engine.Limits(1024*1024,1024*1024);
            var replies = new StringBuilder(); engine.Reply += bytes => replies.Append(Encoding.ASCII.GetString(bytes));
            engine.Feed("\x1b[?1h\x1b[?1s\x1b[?1l\x1b[?1r");
            Check(Encoding.ASCII.GetString(engine.Key(0x25,0,1)) == "\x1bOD", "Real VT mode save/restore preserves application cursor encoding");
            replies.Clear(); engine.Feed("\x1b[?1$p"); Check(replies.ToString() == "\x1b[?1;1$y", "Real VT mode query reports restored state");
            replies.Clear(); engine.Feed("\x1b[6n"); Check(replies.ToString() == "\x1b[1;1R", "Real VT cursor position report matches fresh screen");
            engine.Feed("\x1b[2J\x1b[HABCDE\x1b[90;2;2;3;4$x");
            Check(engine.Capture().Text[81] != "Z" && engine.ScreenText().Contains("ABCDE"), "Known engine gap: DECFRA is ignored, not advertised as supported");
            engine.Feed("\x1b[2;1H12345\x1b[3;1H67890\x1b[2;2;3;4$z");
            Check(engine.Capture().Text[81] == "2" && engine.Capture().Text[163] == "9", "Known engine gap: DECERA leaves the target rectangle unchanged");
            metrics["knownUnsupported"] = new[] { "DECFRA (CSI Pc;Pt;Pl;Pb;Pr $ x) does not fill the rectangle", "DECERA (CSI Pt;Pl;Pb;Pr $ z) does not erase the rectangle" };
            engine.Feed("\x1b[2J\x1b[H\x1b(0lqk\x1b(B"); Check(engine.ScreenText().Contains("\u250c\u2500\u2510"), "DEC special graphics charset reaches Unicode render cells");
            for (int i = 0; i < 150; i++)
            {
                engine.Feed("\r\nrow-" + i + " \u4e2d\u6587 \ud83d\ude80"); engine.Select(0,0,8,0); engine.Find("row", true);
                engine.Feed("\x1b[?2026h"); engine.Resize(40 + i % 60, 12 + i % 20,9,20); engine.Feed("\x1b[?2026l");
                if (i % 5 == 0) engine.Feed("\x1b[?1049hALT\x1b[?1049l");
                _ = engine.Text(true); _ = engine.Capture();
            }
            engine.Feed("\r\nVERIFY_RESIZE_END"); Check(engine.Text().Contains("VERIFY_RESIZE_END"), "Selection/search/synchronized output survive 150 resize/screen-switch iterations");
            var watch = Stopwatch.StartNew(); long bytes = 0; var timings = new List<double>();
            byte[] batch = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("load \u4e2d\u6587 \ud83d\ude80 \x1b[38;2;10;90;200mRGB\x1b[0m\r\n",100)));
            var processBefore = ProcessSnapshot();
            for (int i=0; i<400; i++) { var frame = Stopwatch.StartNew(); engine.Feed(batch,batch.Length); if(i%4==0)_=engine.Capture(); timings.Add(frame.Elapsed.TotalMilliseconds); bytes+=batch.Length; }
            watch.Stop(); engine.Feed("VERIFY_LOAD_END"); Check(engine.Text().Contains("VERIFY_LOAD_END"), "Bounded-history engine remains usable after 40,000 Unicode/color output rows");
            timings.Sort(); metrics["load"] = new { rows=40000, bytes, elapsedMilliseconds=watch.Elapsed.TotalMilliseconds, megabytesPerSecond=bytes/1048576.0/watch.Elapsed.TotalSeconds, feedCaptureP95Milliseconds=timings[(int)(timings.Count*.95)], processBefore, processAfter=ProcessSnapshot(), historyLimitBytes=1048576, coverage="Bounded local burst, not a long-running UI/GPU benchmark" };
            for(int i=0;i<100;i++) engine.Feed(GraphicsTests.Command("a=T,f=100,i=900,q=2",new byte[]{0,1,2,(byte)i}));
            engine.Feed("\x1b_Ga=T,f=32,s=999999999,v=999999999;iAmNotBase64\x1b\\"); engine.Feed("\u001bcVERIFY_MALFORMED_END");
            Check(engine.ScreenText().Contains("VERIFY_MALFORMED_END"), "Malformed PNG/base64/dimension traffic does not prevent reset/recovery");
        }
        File.WriteAllText(Path.Combine(artifacts,"verification-metrics.json"),JsonSerializer.Serialize(metrics,new JsonSerializerOptions{WriteIndented=true}));
        await Applications(artifacts);
    }
    internal static async Task Applications(string artifacts)
    {
        string script = "[Console]::Write([char]27+'[?1049hVERIFY_SHELL'+[char]27+'[?1049l'); [Console]::WriteLine('VERIFY_SHELL_DONE'); exit 0";
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        await RunProgram("powershell.exe -NoLogo -NoProfile -EncodedCommand " + encoded,"VERIFY_SHELL_DONE",artifacts,"powershell5");
        string pwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"PowerShell","7","pwsh.exe");
        if(File.Exists(pwsh)) await RunProgram("\""+pwsh+"\" -NoLogo -NoProfile -EncodedCommand "+encoded,"VERIFY_SHELL_DONE",artifacts,"powershell7");
        else throw new InvalidOperationException("PowerShell 7 unavailable; its verification was not performed.");
        await RunProgram("cmd.exe /d /q /c echo VERIFY_CMD_DONE","VERIFY_CMD_DONE",artifacts,"cmd");
        // Commands use only controlled literals, an isolated tmux socket, and
        // timeout. They do not attach to or terminate the user's sessions.
        await RunProgram("wsl.exe --exec bash -lc \"printf 'VERIFY_BASH_DONE\\n'\"","VERIFY_BASH_DONE",artifacts,"wsl-bash");
        await RunProgram("wsl.exe --exec sh -lc \"printf 'VERIFY_SH_DONE\\n'\"","VERIFY_SH_DONE",artifacts,"wsl-sh");
        await RunProgram("wsl.exe --exec timeout -k 2s 12s vim -Nu NONE -n -i NONE -c \"call setline(1, 'VERIFY_VIM')\" -c redraw -c \"sleep 300m\" -c \"qa!\"","VERIFY_VIM",artifacts,"vim");
        await RunProgram("wsl.exe --exec sh -c \"(echo VERIFY_LESS; seq 1 200) | less -R\"","VERIFY_LESS",artifacts,"less","q");
        await RunProgram("wsl.exe --exec python3 -q","VERIFY_REPL_DONE",artifacts,"python-repl","print('VERIFY_REPL_DONE')\rexit()\r",">>>");
        string socket = "ghostty-verify-" + Guid.NewGuid().ToString("N");
        await RunProgram("wsl.exe --exec timeout -k 2s 12s tmux -L "+socket+" -f /dev/null new-session \"printf VERIFY_TMUX; sleep 0.3\"","VERIFY_TMUX",artifacts,"tmux");
    }
}
