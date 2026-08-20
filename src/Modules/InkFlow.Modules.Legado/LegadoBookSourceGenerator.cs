using System.Text.Json.Serialization;

namespace InkFlow.Modules.Legado;

public sealed class LegadoBookSourceGenerator
{
    public LegadoBookSource Generate(Uri publicBaseUri)
    {
        ArgumentNullException.ThrowIfNull(publicBaseUri);
        if (!publicBaseUri.IsAbsoluteUri || (publicBaseUri.Scheme != Uri.UriSchemeHttp && publicBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Legado public base URI must be absolute HTTP/HTTPS.", nameof(publicBaseUri));
        }

        var baseUrl = publicBaseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        return new LegadoBookSource
        {
            BookSourceName = "墨流 / InkFlow",
            BookSourceGroup = "InkFlow",
            BookSourceUrl = baseUrl,
            SearchUrl = $"{baseUrl}/api/legado/v1/search?q={{{{key}}}}",
            LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RuleSearch = new()
            {
                BookList = "$[*]",
                Name = "$.name",
                Author = "$.author",
                BookUrl = "$.bookUrl",
                CoverUrl = "$.coverUrl",
                Intro = "$.intro",
                LastChapter = "$.lastChapter"
            },
            RuleBookInfo = new()
            {
                Name = "$.name",
                Author = "$.author",
                BookUrl = "$.bookUrl",
                TocUrl = "$.tocUrl",
                CoverUrl = "$.coverUrl",
                Intro = "$.intro",
                LastChapter = "$.lastChapter"
            },
            RuleToc = new()
            {
                ChapterList = "$[*]",
                ChapterName = "$.name",
                ChapterUrl = "$.url"
            },
            RuleContent = new() { Content = "$.content" }
        };
    }
}

public sealed class LegadoBookSource
{
    [JsonPropertyName("bookSourceName")] public string BookSourceName { get; init; } = string.Empty;
    [JsonPropertyName("bookSourceGroup")] public string BookSourceGroup { get; init; } = string.Empty;
    [JsonPropertyName("bookSourceType")] public int BookSourceType { get; init; }
    [JsonPropertyName("bookSourceUrl")] public string BookSourceUrl { get; init; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
    [JsonPropertyName("enabledExplore")] public bool EnabledExplore { get; init; }
    [JsonPropertyName("enabledCookieJar")] public bool EnabledCookieJar { get; init; }
    [JsonPropertyName("searchUrl")] public string SearchUrl { get; init; } = string.Empty;
    [JsonPropertyName("lastUpdateTime")] public long LastUpdateTime { get; init; }
    [JsonPropertyName("respondTime")] public int RespondTime { get; init; } = 180000;
    [JsonPropertyName("ruleSearch")] public LegadoSearchRule RuleSearch { get; init; } = new();
    [JsonPropertyName("ruleBookInfo")] public LegadoBookInfoRule RuleBookInfo { get; init; } = new();
    [JsonPropertyName("ruleToc")] public LegadoTocRule RuleToc { get; init; } = new();
    [JsonPropertyName("ruleContent")] public LegadoContentRule RuleContent { get; init; } = new();
}

public sealed class LegadoSearchRule
{
    [JsonPropertyName("bookList")] public string BookList { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("author")] public string Author { get; init; } = string.Empty;
    [JsonPropertyName("bookUrl")] public string BookUrl { get; init; } = string.Empty;
    [JsonPropertyName("coverUrl")] public string CoverUrl { get; init; } = string.Empty;
    [JsonPropertyName("intro")] public string Intro { get; init; } = string.Empty;
    [JsonPropertyName("lastChapter")] public string LastChapter { get; init; } = string.Empty;
}

public sealed class LegadoBookInfoRule
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("author")] public string Author { get; init; } = string.Empty;
    [JsonPropertyName("bookUrl")] public string BookUrl { get; init; } = string.Empty;
    [JsonPropertyName("tocUrl")] public string TocUrl { get; init; } = string.Empty;
    [JsonPropertyName("coverUrl")] public string CoverUrl { get; init; } = string.Empty;
    [JsonPropertyName("intro")] public string Intro { get; init; } = string.Empty;
    [JsonPropertyName("lastChapter")] public string LastChapter { get; init; } = string.Empty;
}

public sealed class LegadoTocRule
{
    [JsonPropertyName("chapterList")] public string ChapterList { get; init; } = string.Empty;
    [JsonPropertyName("chapterName")] public string ChapterName { get; init; } = string.Empty;
    [JsonPropertyName("chapterUrl")] public string ChapterUrl { get; init; } = string.Empty;
}

public sealed class LegadoContentRule
{
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
}
