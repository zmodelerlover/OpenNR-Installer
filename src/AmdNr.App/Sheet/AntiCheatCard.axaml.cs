using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AmdNr.App;

public partial class AntiCheatCard : UserControl
{
    public AntiCheatCard() => InitializeComponent();

    /// <summary>"Use NRLive": the NRLive page with this game picked.</summary>
    public event Action? Use;

    /// <summary>"Ignore": never shown for this game again.</summary>
    public event Action? Ignore;

    /// <summary>Names what was found: "Easy Anti-Cheat", or "BattlEye, Easy Anti-Cheat" for a game with two.</summary>
    public void Show(IReadOnlyList<string> found)
    {
        Title.Text = Ui.Format("Str.AntiCheatTitle", string.Join(", ", found));
        IsVisible = found.Count > 0;
    }

    private void OnUse(object? sender, RoutedEventArgs e) => Use?.Invoke();

    private void OnIgnore(object? sender, RoutedEventArgs e) => Ignore?.Invoke();
}
