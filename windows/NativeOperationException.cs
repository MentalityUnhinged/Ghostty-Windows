namespace GhosttyWindows;

sealed class NativeOperationException(string operation, int result) : InvalidOperationException(operation + ": " + Explain(result))
{
    public int Result { get; } = result;
    static string Explain(int result) => result switch
    {
        -1 => "native allocation failed",
        -2 => "native argument was invalid",
        -3 => "native buffer capacity was insufficient",
        -4 => "native value is absent",
        -5 => "native IO operation failed",
        -6 => "native resource limit was exceeded",
        -7 => "native safety policy rejected the operation",
        _ => "unknown native failure (" + result + ")"
    };
}
