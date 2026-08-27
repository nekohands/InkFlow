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
    public const string PersonalTokenHeader = "X-InkFlow-Legado-Token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Generate(string baseUrl, string? legadoToken = null)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("baseUrl must be an absolute http(s) URL.", nameof(baseUrl));
        }

        if (legadoToken is not null &&
            (string.IsNullOrWhiteSpace(legadoToken) ||
             legadoToken.Length > 512 ||
             legadoToken.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException(
                "legadoToken must be a non-whitespace value of at most 512 characters.",
                nameof(legadoToken));
        }

        var root = baseUrl.TrimEnd('/');
        var isPersonal = legadoToken is not null;
        var routePrefix = isPersonal
            ? LegadoRoutePrefixes.Personal
            : LegadoRoutePrefixes.Public;
        var header = isPersonal
            ? JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    [PersonalTokenHeader] = legadoToken!,
                },
                JsonOptions)
            : null;

        var manifest = new
        {
            bookSourceName = isPersonal ? "InkFlow / 墨流（个人）" : "InkFlow / 墨流",
            bookSourceGroup = isPersonal ? "个人" : "官方",
            bookSourceUrl = isPersonal ? $"{root}/personal" : root,
            bookSourceType = 0, // 文本
            enabled = true,
            enabledExplore = false,
            header,
            searchUrl = $"{root}{routePrefix}/search?q={{{{key}}}}",
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
