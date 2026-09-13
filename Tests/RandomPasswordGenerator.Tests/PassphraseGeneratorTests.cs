using PasswordStrength;
using RandomPasswordGenerator.Models;
using RandomPasswordGenerator.Services;

namespace RandomPasswordGenerator.Tests;

public class PassphraseGeneratorTests
{
    private static readonly PasswordStrengthService Strength = new();
    private static readonly EmbeddedWordList Words = EmbeddedWordList.Load();
    private readonly PassphraseGenerator _generator = new(Words, Strength);

    [Fact]
    public void 埋め込み辞書は5872語で重複なく小文字英字のみ()
    {
        Assert.Equal(5872, Words.Count);
        var all = Enumerable.Range(0, Words.Count).Select(i => Words[i]).ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.All(all, w => Assert.True(w.Length > 0 && w.All(char.IsAsciiLetterLower), w));
    }

    [Theory]
    [InlineData(4, 50.08)]
    [InlineData(5, 62.60)]
    [InlineData(6, 75.12)]
    [InlineData(7, 87.64)]
    public void 固定区切りのエントロピーは語数掛けるlog2辞書サイズ(int words, double expected)
    {
        var generated = _generator.Generate(new PassphraseOptions { WordCount = words, Separator = PassphraseSeparator.Hyphen });

        Assert.Equal(PasswordStrengthSource.GeneratedExact, generated.Strength!.Source);
        Assert.Equal(expected, generated.Strength.ExactEntropyBits!.Value, 2);
        Assert.Equal(words, generated.Segments.Count);
        Assert.Equal(words - 1, generated.Text.Count(c => c == '-'));
    }

    [Theory]
    [InlineData(PassphraseSeparator.Hyphen, "-")]
    [InlineData(PassphraseSeparator.Space, " ")]
    [InlineData(PassphraseSeparator.Period, ".")]
    [InlineData(PassphraseSeparator.Comma, ",")]
    [InlineData(PassphraseSeparator.Underscore, "_")]
    public void 固定区切りは最後の単語以外に付く(PassphraseSeparator separator, string expected)
    {
        var generated = _generator.Generate(new PassphraseOptions { WordCount = 4, Separator = separator });

        Assert.Equal(expected, generated.Segments[0].Suffix);
        Assert.Equal(expected, generated.Segments[2].Suffix);
        Assert.Equal(string.Empty, generated.Segments[3].Suffix);
        Assert.Equal(string.Concat(generated.Segments.Select(s => s.Text + s.Suffix)), generated.Text);
    }

    [Fact]
    public void ランダムな数字の区切りは区切りごとにlog2_10を加算()
    {
        var generated = _generator.Generate(new PassphraseOptions { WordCount = 4, Separator = PassphraseSeparator.RandomDigit });

        Assert.Equal(4 * Math.Log2(5872) + 3 * Math.Log2(10), generated.Strength!.ExactEntropyBits!.Value, 6);
        Assert.All(generated.Segments.Take(3), s => Assert.True(s.Suffix.Length == 1 && char.IsAsciiDigit(s.Suffix[0]), s.Suffix));
    }

    [Fact]
    public void ランダムな数字と記号の区切りは区切りごとにlog2_42を加算()
    {
        var generated = _generator.Generate(new PassphraseOptions { WordCount = 5, Separator = PassphraseSeparator.RandomDigitOrSymbol });

        Assert.Equal(5 * Math.Log2(5872) + 4 * Math.Log2(42), generated.Strength!.ExactEntropyBits!.Value, 6);
        Assert.All(generated.Segments.Take(4), s => Assert.Contains(s.Suffix[0], CharacterSets.DigitsAndSymbols));
    }

    [Theory]
    [InlineData(true, true, "Sakura")]
    [InlineData(true, false, "SAKURA")]
    [InlineData(false, true, "sakura")]
    [InlineData(false, false, "sakura")]
    public void 大文字小文字の指定(bool upper, bool lower, string expected)
    {
        Assert.Equal(expected, PassphraseGenerator.ApplyCasing("sakura", new PassphraseOptions { IncludeUppercase = upper, IncludeLowercase = lower }));
    }

    [Fact]
    public void 大文字化はエントロピーに影響しない()
    {
        var lower = _generator.Generate(new PassphraseOptions { WordCount = 4, IncludeUppercase = false, IncludeLowercase = true });
        var title = _generator.Generate(new PassphraseOptions { WordCount = 4, IncludeUppercase = true, IncludeLowercase = true });
        Assert.Equal(lower.Strength!.ExactEntropyBits, title.Strength!.ExactEntropyBits);
    }

    [Fact]
    public void 単語は辞書の語である()
    {
        var dictionary = new HashSet<string>(Enumerable.Range(0, Words.Count).Select(i => Words[i]));
        var generated = _generator.Generate(new PassphraseOptions { WordCount = 10 });
        Assert.All(generated.Segments, s => Assert.Contains(s.Text, dictionary));
        Assert.All(generated.Segments, s => Assert.Equal(CharacterKind.Word, s.Kind));
    }

    [Fact]
    public void テキストから辞書を作るとき空行と不正な行は除く()
    {
        var list = EmbeddedWordList.FromText("neko\n\nSakura\nyume\nneko\n123\n");
        Assert.Equal(2, list.Count);
        Assert.Equal("neko", list[0]);
        Assert.Equal("yume", list[1]);
    }
}
