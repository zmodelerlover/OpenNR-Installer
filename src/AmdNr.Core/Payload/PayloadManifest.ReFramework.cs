// REFramework in the payload list, under releases.reframework, where the installers already published never look:
// each release carries REFramework's licence as the "reframework" component, at the release's version, and one
// component per build ("reframework-re9": its dinput8.dll), each at the REFramework version pinned for that game.
// tools/pin-reframework.ps1 writes them.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>The release list's key, and the component with REFramework's LICENSE in it.</summary>
    public const string ReFrameworkComponent = "reframework";

    /// <summary>The component of one REFramework build, by the build's name ("RE9" -> "reframework-re9").</summary>
    public static string ReFrameworkBuildComponent(string build) => $"{ReFrameworkComponent}-{Engine.Lower(build.Trim())}";

    private static bool IsReFrameworkBuild(string component) =>
        component.StartsWith(ReFrameworkComponent + "-", StringComparison.Ordinal);

    /// <summary>The newest release that carries this build, or null.</summary>
    public ComponentRelease? ReFrameworkRelease(string build) =>
        Offered(ReFrameworkComponent).FirstOrDefault(r => r.Components.ContainsKey(ReFrameworkBuildComponent(build)));

    /// <summary>This manifest with REFramework's licence and this build in it, from the newest release that carries
    /// the build; unchanged when none does. Any other build drops out, so the pins name one.</summary>
    public PayloadManifest WithReFramework(string build)
    {
        if (ReFrameworkRelease(build) is not { } release) return this;
        var name = ReFrameworkBuildComponent(build);
        var bare = Without([.. Components.Keys.Where(IsReFrameworkBuild)]);
        return bare.With(new ComponentRelease
        {
            Version = release.Version,
            Components = new Dictionary<string, PayloadComponent>(StringComparer.Ordinal)
            {
                [ReFrameworkComponent] = release.Components[ReFrameworkComponent],
                [name] = release.Components[name],
            },
        });
    }

    private PayloadPins WithReFrameworkPins(PayloadPins pins)
    {
        if (!Components.TryGetValue(ReFrameworkComponent, out var licence)) return pins;
        if (Components.FirstOrDefault(c => IsReFrameworkBuild(c.Key)) is not { Value: not null } build) return pins;
        pins.ReFrameworkBuild = build.Key;
        pins.ReFrameworkVersion = build.Value.Version;
        pins.ReFrameworkFiles = licence.Installed.Concat(build.Value.Installed)
            .ToDictionary(f => f.RelativePath, f => Engine.Lower(f.Sha256), StringComparer.Ordinal);
        return pins;
    }
}
