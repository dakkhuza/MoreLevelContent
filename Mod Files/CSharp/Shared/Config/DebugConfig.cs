
public struct DebugConfig
{
    public bool Verbose;
    public bool Internal;

    public static DebugConfig GetDefault()
    {
        return new DebugConfig()
        {
            Verbose = false,
            Internal = false
        };
    }

    public override string ToString() =>
        $"\n-Debug Config-\n" +
        $"Verbose: {Verbose}\n" +
        $"Internal: {Internal}\n";

}