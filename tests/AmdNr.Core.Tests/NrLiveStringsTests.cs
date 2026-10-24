using System.Text.RegularExpressions;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>The NRLive page's strings sit in Languages\NrLive\, a file per language that its Strings.*.axaml merges.
/// A key missing from one of them shows on screen as its own name, and the settings rows take their labels from
/// NrLiveConfig.Settings by name, so both are checked here, against the source files.</summary>
public partial class NrLiveStringsTests
{
    private static string Languages()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AmdNrInstaller.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "AmdNr.App", "Languages");
    }

    [GeneratedRegex("x:Key=\"([^\"]+)\"")]
    private static partial Regex Key();

    private static List<string> Keys(string file) => Key().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).ToList();

    [Fact]
    public void EveryLanguageHasEveryKeyInTheSameOrderAndMergesIt()
    {
        var languages = Languages();
        var english = Keys(Path.Combine(languages, "NrLive", "NrLive.en.axaml"));
        var strings = Directory.GetFiles(languages, "Strings.*.axaml");
        Assert.Equal(21, strings.Length);
        foreach (var file in strings)
        {
            var code = Path.GetFileName(file)["Strings.".Length..^".axaml".Length];
            var own = Path.Combine(languages, "NrLive", $"NrLive.{code}.axaml");
            Assert.True(File.Exists(own), $"{own} is missing");
            Assert.Equal(english, Keys(own));
            Assert.Contains($"<ResourceInclude Source=\"/Languages/NrLive/NrLive.{code}.axaml\" />", File.ReadAllText(file));
            Assert.Empty(Keys(file).Intersect(english));
        }
    }

    [Fact]
    public void EverySettingHasItsLabelAndEachEntryOfItsMenu()
    {
        var english = Keys(Path.Combine(Languages(), "NrLive", "NrLive.en.axaml"));
        foreach (var setting in NrLiveConfig.Settings)
        {
            Assert.Contains(setting.LabelKey, english);
            for (var i = 0; i < setting.Choices; i++) Assert.Contains(setting.ChoiceKey(i), english);
            if (setting.Hint) Assert.Contains(setting.HintKey, english);
        }
    }
}
