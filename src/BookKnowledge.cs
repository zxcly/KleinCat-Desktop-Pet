using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace KleinPet;

internal sealed class BookKnowledge
{
    private sealed record Passage(string Chapter, string Text);
    private readonly List<Passage> passages = new();
    private readonly Dictionary<string, int> documentFrequency = new();
    private static readonly Lazy<BookKnowledge> Instance = new(() => new BookKnowledge());
    private static readonly Regex LinkPattern = new("<a[^>]+href=\"([^\"]+\\.xhtml)\"[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParagraphPattern = new("<p(?:\\s[^>]*)?>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagPattern = new("<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);
    internal static string BookPath => Path.Combine(Program.DataDir, "books", "诡秘之主.epub");
    internal static bool Available => File.Exists(BookPath);
    internal static int PassageCount => Instance.Value.passages.Count;

    private BookKnowledge()
    {
        if (!Available) return;
        using var book = ZipFile.OpenRead(BookPath);
        var catalog = book.GetEntry("catalog.xhtml");
        if (catalog == null) return;
        using var reader = new StreamReader(catalog.Open(), Encoding.UTF8);
        var links = LinkPattern.Matches(reader.ReadToEnd());
        var chapterNumber = 0;
        foreach (Match link in links)
        {
            var entry = book.GetEntry(link.Groups[1].Value);
            if (entry == null) continue;
            chapterNumber++;
            using var chapterReader = new StreamReader(entry.Open(), Encoding.UTF8);
            var chapterText = chapterReader.ReadToEnd();
            var chapterTitle = Clean(link.Groups[2].Value);
            var label = $"目录第{chapterNumber}篇 · {chapterTitle}";
            var current = new StringBuilder();
            foreach (Match paragraph in ParagraphPattern.Matches(chapterText))
            {
                var text = Clean(paragraph.Groups[1].Value);
                if (text.Length < 8) continue;
                if (current.Length > 0 && current.Length + text.Length > 520)
                {
                    Add(label, current.ToString());
                    current.Clear();
                }
                if (text.Length > 620)
                {
                    for (var offset = 0; offset < text.Length; offset += 520)
                        Add(label, text.Substring(offset, Math.Min(560, text.Length - offset)));
                }
                else current.Append(text).Append('\n');
            }
            if (current.Length > 0) Add(label, current.ToString());
        }
        foreach (var passage in passages)
            foreach (var term in Terms(passage.Text).Distinct())
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
    }

    private void Add(string chapter, string text) => passages.Add(new Passage(chapter, text.Trim()));
    private static string Clean(string html) => Regex.Replace(WebUtility.HtmlDecode(TagPattern.Replace(html, "")), @"\s+", " ").Trim();

    private static IEnumerable<string> Terms(string text)
    {
        var normalized = Regex.Replace(text, @"[^\p{L}\p{N}]", "");
        for (var length = 2; length <= 3; length++)
            for (var i = 0; i + length <= normalized.Length; i++)
                yield return normalized.Substring(i, length);
    }

    internal static bool RelevantQuestion(string question)
    {
        var hints = new[] { "原著", "书里", "书中", "小说", "剧情", "章节", "诡秘", "克莱恩", "愚者", "塔罗会", "值夜者", "灰雾", "阿兹克", "奥黛丽", "伦纳德", "梅丽莎", "班森", "格尔曼", "夏洛克", "邓恩", "黑夜女神", "非凡者", "序列", "占卜家" };
        return hints.Any(hint => question.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? FindContext(string question)
    {
        if (!Available || !RelevantQuestion(question)) return null;
        var index = Instance.Value;
        if (index.passages.Count == 0) return null;
        var terms = Terms(question).Distinct().Where(term => index.documentFrequency.GetValueOrDefault(term) > 0).ToArray();
        if (terms.Length == 0) return null;
        var asksAboutLetters = question.Contains("写信", StringComparison.Ordinal)
            || question.Contains("来信", StringComparison.Ordinal)
            || question.Contains("信件", StringComparison.Ordinal);
        var letterTerms = new[] { "写信", "来信", "信件", "回信", "寄信" };
        var ranked = index.passages.Select((passage, id) =>
        {
            var score = 0.0;
            foreach (var term in terms)
            {
                var weight = Math.Log(1.0 + index.passages.Count / (double)index.documentFrequency[term]);
                if (passage.Text.Contains(term, StringComparison.Ordinal)) score += weight;
                if (passage.Chapter.Contains(term, StringComparison.Ordinal)) score += weight * 0.25;
            }
            if (asksAboutLetters)
                score = letterTerms.Any(term => passage.Text.Contains(term, StringComparison.Ordinal)) ? score + 25 : score * 0.1;
            return (passage, id, score);
        }).Where(item => item.score > 0).OrderByDescending(item => item.score).Take(24).ToArray();
        if (ranked.Length == 0) return null;
        var chosen = new List<(Passage passage, int id, double score)>();
        foreach (var item in ranked)
        {
            if (chosen.Any(previous => Math.Abs(previous.id - item.id) <= 1)) continue;
            chosen.Add(item);
            if (chosen.Count == 3) break;
        }
        return string.Join("\n\n", chosen.Select(item => $"【{item.passage.Chapter}】\n{item.passage.Text}"));
    }
}
