using System.IO;

namespace GhosttyWindows;

static class ShellFiles
{
    internal static string Quote(string profile, IEnumerable<string> paths, Func<string, string>? wslPath = null)
    {
        var result = new List<string>();
        foreach (string original in paths)
        {
            if (!Path.IsPathFullyQualified(original) || original.Any(c => c < ' ' || c == '\x7f' || c == '"')) throw new ArgumentException("Dropped files must be absolute paths without quotes or control characters.");
            if (profile == "PowerShell") result.Add("'" + original.Replace("'", "''") + "'");
            else if (profile == "WSL")
            {
                string path = wslPath?.Invoke(original) ?? throw new ArgumentException("WSL paths must be resolved by the selected WSL environment before quoting.");
                if (!path.StartsWith('/') || path.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException("WSL returned an invalid translated path.");
                result.Add("'" + path.Replace("'", "'\\''") + "'");
            }
            else if (profile == "Command Prompt")
            {
                // CMD expands %...% and, when enabled, !...! even inside quotes.
                // Refuse those spellings instead of altering the selected file name.
                if (original.Contains('%') || original.Contains('!')) throw new ArgumentException("CMD may expand % or ! in a file path. Use PowerShell or paste the path manually after choosing the appropriate CMD expansion mode.");
                result.Add("\"" + original + "\"");
            }
            else throw new ArgumentException("File-drop quoting is unavailable for this shell profile.");
        }
        return string.Join(" ", result);
    }
    internal static async Task<string> QuoteDrop(string profile, string[] paths, CancellationToken cancellation)
    {
        if (profile != "WSL") return Quote(profile, paths);
        var translated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            // Validate the Windows source before launching a translator.
            _ = Quote("PowerShell", new[] { path });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (string argument in new[] { "--exec", "wslpath", "-a", "-u", path }) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start) ?? throw new IOException("Could not start WSL path translation.");
            process.StandardInput.Close(); // GUI launches have no inherited console input handle.
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var error = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                string result = (await output).TrimEnd('\r', '\n'), reason = await error;
                if (process.ExitCode != 0) throw new IOException("WSL path translation failed: " + reason.TrimEnd());
                translated[path] = result;
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            { throw new IOException("WSL path translation timed out. Start the distribution and retry; no file paths were inserted."); }
            finally
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync();
                try { await Task.WhenAll(output, error); } catch (OperationCanceledException) { }
            }
        }
        return Quote(profile, paths, path => translated[path]);
    }

}
