// OpenRR in the payload list (Work.OpenRr.cs): its runtime and kernels, and NVIDIA's nvngx_dlssd.dll 310.9.1.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>OpenRR, this project's Ray Reconstruction runtime for RX 9000: OpenNrRrRuntime.dll and its kernels, in
    /// opennr-rr.shaders/ in the payload and opennr-rr\shaders\ in the game. Inside an OptiScaler release only, and
    /// downloaded only when OpenRR is ticked.</summary>
    public const string OpenRrComponent = "openrr";

    /// <summary>NVIDIA's nvngx_dlssd.dll 310.9.1, the only one OpenRR builds its network from, for a game that has
    /// another or none (Work.OpenRrNeedsDlssd).</summary>
    public const string OpenRrDlssdComponent = "openrr-dlssd";

    /// <summary>OpenRR's kernels for RX 7000 and RDNA3 / RDNA 3.5 graphics (OpenRR 0.1.6 on, experimental), in
    /// opennr-rr.shaders-gfx11/ in the payload and opennr-rr\shaders-gfx11\ in the game, beside <see cref="OpenRrComponent"/>.
    /// A component of its own for <see cref="LmxxfGfx1200Component"/>'s reason, downloaded only on such a card.</summary>
    public const string OpenRrGfx11Component = "openrr-gfx11";

    /// <summary>The OpenRR versions a player can pick, newest first: the ones the OptiScaler releases carry.</summary>
    public IReadOnlyList<PayloadComponent> OpenRrVersions() =>
        (Releases?.GetValueOrDefault(OptiScalerComponent) ?? [])
        .Where(r => !IsPlaceholder(r))
        .Select(r => r.Components.GetValueOrDefault(OpenRrComponent)).OfType<PayloadComponent>()
        .DistinctBy(c => c.Version)
        .OrderByDescending(c => AddonReleases.Version(c.Version) ?? new Version())
        .ToList();

    /// <summary>This manifest with OpenRR at a version a player picked. Null, a version this payload does not list, or
    /// an OptiScaler release without OpenRR keeps what the release pins.</summary>
    public PayloadManifest WithOpenRrVersion(string? version) =>
        version is null || !Components.ContainsKey(OpenRrComponent)
        || OpenRrVersions().FirstOrDefault(c => c.Version == version) is not { } picked
            ? this
            : Merge(new ComponentRelease
            {
                Version = version,
                Components = new Dictionary<string, PayloadComponent>(StringComparer.Ordinal) { [OpenRrComponent] = picked },
            });

    private PayloadPins WithOpenRr(PayloadPins pins)
    {
        if (Components.TryGetValue(OpenRrComponent, out var rr))
        {
            var gfx11 = Components.TryGetValue(OpenRrGfx11Component, out var g) ? g.Installed : [];
            pins.OpenRrFiles = rr.Installed.Concat(gfx11).ToDictionary(f => f.RelativePath, f => Engine.Lower(f.Sha256), StringComparer.Ordinal);
            pins.OpenRrVersion = rr.Version;
        }
        if (Components.TryGetValue(OpenRrDlssdComponent, out var dlssd) && dlssd.Installed.FirstOrDefault() is { } file)
            (pins.OpenRrDlssd, pins.OpenRrDlssdSha) = (file.RelativePath, Engine.Lower(file.Sha256));
        return pins;
    }
}
