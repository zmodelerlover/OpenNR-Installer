using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AmdNr.App;

public partial class ReFrameworkCard : UserControl
{
    /// <summary>REFramework's source, which the credit and the credits panel open.</summary>
    public const string SourceUrl = "https://github.com/praydog/REFramework";

    public ReFrameworkCard() => InitializeComponent();

    /// <summary>The box was clicked, with its new state.</summary>
    public event Action<bool>? Changed;

    /// <summary>Draws the card: the box, the warning for a game that plays online (empty for none) and the line.</summary>
    public void Show(bool on, bool enabled, string risk, string line)
    {
        Box.IsChecked = on;
        Box.IsEnabled = enabled;
        RiskText.Text = risk;
        RiskBox.IsVisible = risk.Length > 0;
        Line.Text = line;
        Line.IsVisible = line.Length > 0;
        IsVisible = true;
    }

    private void OnClick(object? sender, RoutedEventArgs e) => Changed?.Invoke(Box.IsChecked == true);

    private void OnCredit(object? sender, RoutedEventArgs e) => AppUpdate.OpenInBrowser(SourceUrl);
}
