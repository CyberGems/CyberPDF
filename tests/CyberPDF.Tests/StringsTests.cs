using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using Xunit;

namespace CyberPDF.Tests;

public sealed class StringsTests
{
    [Fact]
    public void EveryKeyHasBothLanguages()
    {
        foreach (var key in Strings.Keys)
        {
            Strings.Current = Lang.En;
            var en = Strings.Get(key);
            Strings.Current = Lang.Es;
            var es = Strings.Get(key);

            Assert.False(string.IsNullOrWhiteSpace(en), $"English text missing for '{key}'.");
            Assert.False(string.IsNullOrWhiteSpace(es), $"Spanish text missing for '{key}'.");
            Assert.DoesNotContain("{" + key + "}", en);
            Assert.DoesNotContain("{" + key + "}", es);
        }
    }

    [Fact]
    public void PlaceholdersMatchBetweenLanguages()
    {
        // A translation that drops {0} would silently lose data at runtime.
        foreach (var key in Strings.Keys)
        {
            Strings.Current = Lang.En;
            var en = Placeholders(Strings.Get(key));
            Strings.Current = Lang.Es;
            var es = Placeholders(Strings.Get(key));

            Assert.Equal(en, es);
        }
    }

    [Fact]
    public void UnknownKeyIsVisibleInsteadOfEmpty()
    {
        var text = Strings.Get("ThisKeyDoesNotExist");
        Assert.Equal("{ThisKeyDoesNotExist}", text);
    }

    [Fact]
    public void FormatSubstitutesArguments()
    {
        Strings.Current = Lang.En;
        Assert.Equal("Page 3 of 10", Strings.Format("PageOf", 3, 10));
        Strings.Current = Lang.Es;
        Assert.Equal("Página 3 de 10", Strings.Format("PageOf", 3, 10));
    }

    private static IEnumerable<int> Placeholders(string text)
    {
        var results = new List<int>();
        for (var i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == '{' && char.IsDigit(text[i + 1]) && text[i + 2] == '}')
            {
                results.Add(text[i + 1] - '0');
            }
        }
        return results;
    }
}
