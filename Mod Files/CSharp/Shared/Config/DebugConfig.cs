using Barotrauma;


public struct ClientConfig
{
    public bool Verbose;
    public bool Internal;

    public static ClientConfig GetDefault()
    {
        return new ClientConfig()
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