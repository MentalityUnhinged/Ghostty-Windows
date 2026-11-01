using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace GhosttyWindows;

// A pane owns one ConPTY, its pipe pumps, and a kill-on-close Job Object.
sealed class PseudoConsole : IDisposable
{
    IntPtr hpc, process, job;
    FileStream? input, output;
    readonly Channel<byte[]> writes = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    Task reader = Task.CompletedTask, writer = Task.CompletedTask, processMonitor = Task.CompletedTask;
    internal const int MaxQueuedBytes = 4 * 1024 * 1024;
    long queuedBytes;
    internal long QueuedInputBytes => Interlocked.Read(ref queuedBytes);
    string? writeFailure;
    int startRequested;
    int disposed;
    public uint ProcessId { get; private set; }
    public Task Cleanup { get; private set; } = Task.CompletedTask;
    public event Action<byte[], int>? Data;
    public event Action<int>? Exited;
    public event Action<string>? Failed;
    public void Start(string command, string directory, int cols, int rows)
    {
        if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(PseudoConsole));
        if (Interlocked.Exchange(ref startRequested, 1) != 0) throw new InvalidOperationException("A ConPTY owner can only start once.");
        IntPtr readIn = IntPtr.Zero, writeIn = IntPtr.Zero, readOut = IntPtr.Zero, writeOut = IntPtr.Zero, attrs = IntPtr.Zero, env = IntPtr.Zero;
        PROCESS_INFORMATION pi = default;
        try
        {
            Check(CreatePipe(out readIn, out writeIn, IntPtr.Zero, 0));
            Check(CreatePipe(out readOut, out writeOut, IntPtr.Zero, 0));
            Marshal.ThrowExceptionForHR(CreatePseudoConsole(new COORD((short)cols, (short)rows), readIn, writeOut, 0, out hpc));
            CloseHandle(readIn); readIn = IntPtr.Zero; CloseHandle(writeOut); writeOut = IntPtr.Zero;
            input = new FileStream(new SafeFileHandle(writeIn, true), FileAccess.Write, 4096, false); writeIn = IntPtr.Zero;
            output = new FileStream(new SafeFileHandle(readOut, true), FileAccess.Read, 65536, false); readOut = IntPtr.Zero;
            // Start draining before launching; ConPTY can synchronously flush during startup/close.
            reader = Task.Run(() =>
            {
                var b = new byte[65536];
                try { int n; while ((n = output.Read(b, 0, b.Length)) > 0) Data?.Invoke(b, n); }
                catch (IOException e)
                {
                    bool knownExit = process != IntPtr.Zero && GetExitCodeProcess(process, out uint code) && code != 259;
                    if (Volatile.Read(ref disposed) == 0 && !knownExit) Failed?.Invoke("Terminal output pipe failed: " + e.Message);
                }
                catch (ObjectDisposedException) { }
            });
            writer = Task.Run(async () =>
            {
                try
                {
                    await foreach (var b in writes.Reader.ReadAllAsync())
                    {
                        try { input.Write(b, 0, b.Length); input.Flush(); }
                        finally { Interlocked.Add(ref queuedBytes, -b.Length); }
                    }
                }
                catch (Exception e) when (e is IOException or ObjectDisposedException)
                { if (Volatile.Read(ref disposed) == 0) { Volatile.Write(ref writeFailure, e.Message); Failed?.Invoke("Terminal input pipe failed: " + e.Message); } }
                finally { writes.Writer.TryComplete(); while (writes.Reader.TryRead(out var pending)) Interlocked.Add(ref queuedBytes, -pending.Length); }
            });
            nuint size = 0; InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attrs = Marshal.AllocHGlobal(checked((int)size)); Check(InitializeProcThreadAttributeList(attrs, 1, 0, ref size));
            Check(UpdateProcThreadAttribute(attrs, 0, (IntPtr)0x20016, hpc, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
            var si = new STARTUPINFOEX { StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() }, lpAttributeList = attrs };
            var environment = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!, StringComparer.OrdinalIgnoreCase);
            environment["TERM"] = "xterm-256color"; environment["COLORTERM"] = "truecolor"; environment["TERM_PROGRAM"] = "ghostty-windows"; environment["TERM_PROGRAM_VERSION"] = HostIdentity.Version;
            env = Marshal.StringToHGlobalUni(string.Join('\0', environment.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase).Select(e => e.Key + "=" + e.Value)) + "\0\0");
            Check(CreateProcessW(null, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false, 0x80000 | 0x400 | 4, env, directory, ref si, out pi));
            process = pi.hProcess;
            ProcessId = pi.dwProcessId;
            job = CreateJobObjectW(IntPtr.Zero, null); Check(job != IntPtr.Zero);
            var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION(); limits.BasicLimitInformation.LimitFlags = 0x2000;
            Check(SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()));
            Check(AssignProcessToJobObject(job, process));
            if (ResumeThread(pi.hThread) == uint.MaxValue) throw new Win32Exception();
            CloseHandle(pi.hThread); pi.hThread = IntPtr.Zero;
            var watched = process;
            processMonitor = Task.Run(() => { if (WaitForSingleObject(watched, uint.MaxValue) != 0) throw new Win32Exception(); Check(GetExitCodeProcess(watched, out var code)); if (Volatile.Read(ref disposed) == 0) Exited?.Invoke((int)code); });
        }
        catch { if (pi.hProcess != IntPtr.Zero) TerminateProcess(pi.hProcess, 1); Dispose(); throw; }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (attrs != IntPtr.Zero) { DeleteProcThreadAttributeList(attrs); Marshal.FreeHGlobal(attrs); }
            if (env != IntPtr.Zero) Marshal.FreeHGlobal(env);
            foreach (var h in new[] { readIn, writeIn, readOut, writeOut }) if (h != IntPtr.Zero) CloseHandle(h);
        }
    }
    public void Send(byte[] b)
    {
        if (b.Length == 0 || Volatile.Read(ref disposed) != 0) return;
        if (Volatile.Read(ref writeFailure) is string failure) throw new IOException("Terminal input pipe failed: " + failure);
        long total = Interlocked.Add(ref queuedBytes, b.Length);
        if (total > MaxQueuedBytes) { Interlocked.Add(ref queuedBytes, -b.Length); throw new IOException("Terminal input exceeds the 4 MiB queued-byte limit; wait for the shell to consume input."); }
        if (!writes.Writer.TryWrite(b)) { Interlocked.Add(ref queuedBytes, -b.Length); throw new IOException("Terminal input queue is full or closed; wait for the shell to consume input."); }
    }
    public void Send(string s) => Send(Encoding.UTF8.GetBytes(s));
    public void Resize(int cols, int rows) { if (hpc != IntPtr.Zero && Volatile.Read(ref disposed) == 0) Marshal.ThrowExceptionForHR(ResizePseudoConsole(hpc, new COORD((short)cols, (short)rows))); }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        writes.Writer.TryComplete();
        if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
        var console = hpc; hpc = IntPtr.Zero;
        // ConPTY close may wait for a final pipe flush. The read pump stays alive.
        Cleanup = Task.Run(async () =>
        {
            // Closing ConPTY ends its pipe peers. Keep our streams alive until
            // both pumps settle: concurrent FileStream.Dispose/Read can throw
            // NotSupportedException as well as ObjectDisposedException.
            try
            {
                if (console != IntPtr.Zero) ClosePseudoConsole(console);
                await Task.WhenAll(reader, writer, processMonitor);
            }
            finally
            {
                input?.Dispose(); output?.Dispose();
                if (process != IntPtr.Zero) { Check(CloseHandle(process)); process = IntPtr.Zero; }
            }
        });
    }
    static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    [StructLayout(LayoutKind.Sequential)] struct COORD { public short X, Y; public COORD(short x, short y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct STARTUPINFO { public int cb; public string? lpReserved, lpDesktop, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)] struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public uint dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_BASIC_LIMIT_INFORMATION { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION { public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation; public IO_COUNTERS IoInfo; public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
    [DllImport("kernel32", SetLastError = true)] static extern bool CreatePipe(out IntPtr r, out IntPtr w, IntPtr a, uint size);
    [DllImport("kernel32")] static extern int CreatePseudoConsole(COORD size, IntPtr input, IntPtr output, uint flags, out IntPtr hpc);
    [DllImport("kernel32")] static extern int ResizePseudoConsole(IntPtr hpc, COORD size);
    [DllImport("kernel32")] static extern void ClosePseudoConsole(IntPtr hpc);
    [DllImport("kernel32", SetLastError = true)] static extern bool InitializeProcThreadAttributeList(IntPtr p, int count, int flags, ref nuint size);
    [DllImport("kernel32", SetLastError = true)] static extern bool UpdateProcThreadAttribute(IntPtr p, uint flags, IntPtr attr, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32")] static extern void DeleteProcThreadAttributeList(IntPtr p);
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CreateProcessW(string? app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr environment, string directory, ref STARTUPINFOEX si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr CreateJobObjectW(IntPtr a, string? name);
    [DllImport("kernel32", SetLastError = true)] static extern bool SetInformationJobObject(IntPtr job, int cls, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint size);
    [DllImport("kernel32", SetLastError = true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32")] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32")] static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr h, uint timeout);
    [DllImport("kernel32", SetLastError = true)] static extern bool GetExitCodeProcess(IntPtr process, out uint code);
}
