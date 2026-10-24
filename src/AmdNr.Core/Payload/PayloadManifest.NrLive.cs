// NRLive in the payload list: its package, and whatever else its release carries (the mochizuki models it
// runs NR with), under releases.nrlive, where the installers already published never look.

namespace AmdNr.Core;

public sealed partial class PayloadManifest
{
    /// <summary>NRLive, Caliel's window scaler (GPL-3.0, github.com/Caliel666/NRLive): its portable package as one
    /// zip, which <see cref="NrLivePackage"/> unpacks into this app's data folder. Never installed in a game, and
    /// downloaded only from the NRLive page.</summary>
    public const string NrLiveComponent = "nrlive";

    /// <summary>The NRLive release to offer, newest first, or null when the payload has none -- or only one still
    /// pinned to <see cref="PlaceholderSha"/>, whose zip is not published yet. Checked here rather than through
    /// <see cref="Offered"/> alone, which trusts a version listed under components: a placeholder there would be
    /// downloaded, fail its hash, and say so on every click.</summary>
    public ComponentRelease? NrLive() =>
        Offered(NrLiveComponent).FirstOrDefault(r => !IsPlaceholder(r));
}
