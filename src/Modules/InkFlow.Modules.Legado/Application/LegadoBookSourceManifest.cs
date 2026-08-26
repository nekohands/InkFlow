using System.Text.Json;
using System.Text.Json.Serialization;

namespace InkFlow.Modules.Legado.Application;

/// <summary>
/// 生成 /legado/book-source.json 书源清单。
/// 规则 URL 指向本服务的 Legado v1 契约端点;规则字段使用 JSONPath,
/// 与 InkFlow 的响应形态一一对应。清单由代码生成,不手工维护 JSON。
/// </summary>
public static class LegadoBookSourceManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Generate(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("baseUrl must be an absolute http(s) URL.", nameof(baseUrl));
        }

        var root = baseUrl.TrimEnd('/');

        var manifest = new
        {
            bookSourceName = "InkFlow / 墨流",
            bookSourceGroup = "官方",
            bookSourceUrl = root,
            bookSourceType = 0, // 文本
            enabled = true,
            enabledExplore = false,
            searchUrl = $"{root}/api/legado/v1/search?q={{{{key}}}}",
            ruleSearch = new
            {
                checkKeyWord = "剑来",
                bookList = "$.data[*]",
                name = "$.title",
                author = "$.author",
                bookUrl = "$.detailUrl",
            },
            ruleBookInfo = new
            {
                name = "$.title",
                author = "$.author",
                tocUrl = "$.tocUrl",
            },
            ruleToc = new
            {
                chapterList = "$.data[*]",
                chapterName = "$.title",
                chapterUrl = "$.chapterUrl",
            },
            ruleContent = new
            {
                content = "$.content",
            },
        };

        return JsonSerializer.Serialize(manifest, JsonOptions);
    }
}
