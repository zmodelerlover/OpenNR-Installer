namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>The OptiScaler versions a player can pick: <see cref="Offered"/> without the retired ones. The
    /// top-level pin stays in the manifest for the old apps that read it, but not in the menu.</summary>
    public IReadOnlyList<ComponentRelease> OptiScalerMenu() =>
        Offered(OptiScalerComponent).Where(r => AddonReleases.Version(r.Version) >= OldestOptiScaler).ToList();

    private static readonly Version OldestOptiScaler = new(0, 5, 5);
}
