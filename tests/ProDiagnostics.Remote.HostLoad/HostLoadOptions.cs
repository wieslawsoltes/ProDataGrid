namespace ProDiagnostics.Remote.HostLoad;

internal sealed class HostLoadOptions
{
    public HostLoadProfile Profile { get; init; } = HostLoadProfile.Small;

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(20);

    public static HostLoadOptions Parse(string[] args)
    {
        var profile = HostLoadProfile.Small;
        var duration = TimeSpan.FromSeconds(20);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--profile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                profile = HostLoadProfile.FromName(args[++i]);
                continue;
            }

            if (string.Equals(arg, "--duration-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var seconds) && seconds > 0)
                {
                    duration = TimeSpan.FromSeconds(seconds);
                }
            }
        }

        return new HostLoadOptions
        {
            Profile = profile,
            Duration = duration,
        };
    }
}
