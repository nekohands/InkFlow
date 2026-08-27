using System.Text.Json;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Sources.Adapters.SeventeenK;

/// <summary>
/// 17K 小说网的官方 API CodeAdapter。
///
/// API 返回 JSON 且目录与正文分别使用 API/Web 主机，超出当前 RuleAdapter
/// 的 CSS-only 能力，因此以受信任代码适配器接入。外部标识约定为纯数字
/// bookId；章节标识为 "bookId/chapterId"，由适配器自包含定位。
///
/// 付费章节只读取上游明确标记为免费的正文；未购买的 VIP 章节返回 null，
/// 不尝试绕过登录、订阅或访问控制。
/// </summary>
public sealed class SeventeenKSourceAdapter(
    HttpClient http,
    IIpAddressResolver resolver) : ISourceAdapter
{
    public const string SourceIdValue = "17k";
    public const string DisplayNameValue = "17K小说网";
    public const string BaseUrlValue = "https://www.17k.com";

    private const string SearchApiHost = "api.ali.17k.com";
    private const string ApiHost = "api.17k.com";
    private const string WebHost = "www.17k.com";
    private const string PublicClientParameter = "4037465544";
    private const string ContentClientParameter = "2406394919";

    private static readonly HashSet<string> AllowedHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            SearchApiHost,
            ApiHost,
            WebHost,
        };

    public string SourceId => SourceIdValue;

    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var uri = BuildUri(
            SearchApiHost,
            $"/v2/book/search?sort_type=0&app_key={PublicClientParameter}" +
            $"&_access_version=2&cps=0&channel=2&_versions=1070&merchant=17KH5" +
            $"&page=1&client_type=1&_filter_data=1&class=0&key={Uri.EscapeDataString(keyword.Trim())}");
        using var document = await FetchJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        if (document is null ||
            !TryGetProperty(document.RootElement, "data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceSearchResult>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var externalBookId = ReadString(item, "book_id", "bookId", "id");
            var title = ReadString(item, "book_name", "bookName", "name");
            if (!TryNormalizeNumericId(externalBookId, out externalBookId) ||
                string.IsNullOrWhiteSpace(title) ||
                !seenIds.Add(externalBookId))
            {
                continue;
            }

            results.Add(new SourceSearchResult(
                externalBookId,
                title,
                ReadString(item, "author_name", "authorName", "authorPenName", "author") ?? "未知"));
        }

        return results;
    }

    public async Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeNumericId(externalBookId, out var bookId))
        {
            return null;
        }

        var uri = BuildUri(
            ApiHost,
            $"/book/{bookId}/split1/merge?iltc=1&cpsOpid=0&_filterData=1" +
            $"&device_id=&channel=0&_versions=1160&merchant=17Kyyb&platform=2" +
            $"&manufacturer=Xiaomi&clientType=1&appKey={PublicClientParameter}" +
            "&model=&cpsSource=0&brand=Redmi&youthModel=0");
        using var document = await FetchJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        if (document is null || !TryGetProperty(document.RootElement, "data", out var data))
        {
            return null;
        }

        var bookTop = FindFirstObject(data, "bookTop");
        if (bookTop.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var title = ReadString(bookTop, "bookName", "book_name", "name");
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new SourceBookInfo(
            title,
            ReadString(bookTop, "authorPenName", "author_name", "authorName", "author") ?? "未知");
    }

    public async Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
        string externalBookId,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeNumericId(externalBookId, out var requestedBookId))
        {
            return [];
        }

        var uri = BuildUri(
            ApiHost,
            $"/v2/book/{requestedBookId}/volumes?app_key={PublicClientParameter}" +
            "&price_extend=1&_versions=1070&client_type=2&_filter_data=1&channel=2" +
            $"&merchant=17Khwyysd&_access_version=2&cps=0&book_id={requestedBookId}");
        using var document = await FetchJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        if (document is null ||
            !TryGetProperty(document.RootElement, "data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var bookId = ReadString(data, "book_id", "bookId");
        if (!TryNormalizeNumericId(bookId, out bookId))
        {
            bookId = requestedBookId;
        }

        if (!TryGetProperty(data, "volumes", out var volumes) ||
            volumes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var index = 0;
        var entries = new List<SourceTocEntry>();
        foreach (var volume in volumes.EnumerateArray())
        {
            if (volume.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(volume, "chapters", out var chapters) ||
                chapters.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var chapter in chapters.EnumerateArray())
            {
                if (chapter.ValueKind != JsonValueKind.Object ||
                    !TryNormalizeNumericId(
                        ReadString(chapter, "chapter_id", "chapterId", "id"),
                        out var chapterId))
                {
                    continue;
                }

                var title = ReadString(chapter, "name", "chapter_name", "chapterName");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                entries.Add(new SourceTocEntry(
                    $"{bookId}/{chapterId}",
                    index++,
                    title));
            }
        }

        return entries;
    }

    public async Task<string?> GetChapterContentAsync(
        string externalChapterId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseChapterReference(externalChapterId, out var bookId, out var chapterId))
        {
            return null;
        }

        var uri = BuildUri(
            WebHost,
            $"/ck/book/{bookId}/chapter/{chapterId}?subAllPrice=1&appKey={ContentClientParameter}" +
            $"&bid={bookId}&cid={chapterId}");
        using var document = await FetchJsonAsync(uri, cancellationToken).ConfigureAwait(false);
        if (document is null ||
            !TryGetProperty(document.RootElement, "data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // 对未购买的 VIP 章节保持上游访问控制，不使用返回的订阅/自动购买地址。
        if (TryGetProperty(data, "isVIP", out var vip) || TryGetProperty(data, "isVip", out vip))
        {
            var vipId = ReadString(vip, "id");
            if (vipId == "1" &&
                (!TryGetProperty(data, "userReadInfo", out var readInfo) ||
                 ReadString(readInfo, "free") != "1"))
            {
                return null;
            }
        }

        if (!TryGetProperty(data, "content", out var content))
        {
            return null;
        }

        var paragraphs = new List<string>();
        if (content.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in content.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String
                    ? ReadScalar(item)
                    : item.ValueKind == JsonValueKind.Object
                        ? ReadString(item, "text", "content")
                        : null;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    paragraphs.Add(text);
                }
            }
        }
        else
        {
            var text = ReadScalar(content);
            if (!string.IsNullOrWhiteSpace(text))
            {
                paragraphs.Add(text);
            }
        }

        return paragraphs.Count == 0 ? null : string.Join("\n\n", paragraphs);
    }

    private async Task<JsonDocument?> FetchJsonAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!AllowedHosts.Contains(uri.Host))
        {
            throw new InvalidOperationException($"17k adapter rejected host '{uri.Host}'.");
        }

        var errors = await SsrfGuard
            .InspectResolvedAsync(uri, resolver, cancellationToken)
            .ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"ssrf: {string.Join("; ", errors)}");
        }

        using var response = await http
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri BuildUri(string host, string pathAndQuery) =>
        new($"https://{host}{pathAndQuery}", UriKind.Absolute);

    private static bool TryParseChapterReference(
        string? externalChapterId,
        out string bookId,
        out string chapterId)
    {
        bookId = string.Empty;
        chapterId = string.Empty;
        var segments = externalChapterId?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments is { Length: 2 } &&
               TryNormalizeNumericId(segments[0], out bookId) &&
               TryNormalizeNumericId(segments[1], out chapterId);
    }

    private static bool TryNormalizeNumericId(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 32 ||
            !normalized.All(character => character is >= '0' and <= '9'))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = normalized.TrimStart('0');
        if (normalized.Length == 0)
        {
            normalized = "0";
        }

        return true;
    }

    private static JsonElement FindFirstObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetProperty(item, propertyName, out var value) &&
                    value.ValueKind == JsonValueKind.Object)
                {
                    return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Object &&
                 TryGetProperty(element, propertyName, out var objectValue) &&
                 objectValue.ValueKind == JsonValueKind.Object)
        {
            return objectValue;
        }

        return default;
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryGetProperty(element, propertyName, out var value))
            {
                var result = ReadScalar(value);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result.Trim();
                }
            }
        }

        return null;
    }

    private static string? ReadScalar(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
