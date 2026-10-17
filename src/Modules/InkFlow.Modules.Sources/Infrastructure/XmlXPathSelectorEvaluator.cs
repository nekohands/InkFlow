using System.IO;
using System.Xml;
using System.Xml.XPath;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>Evaluates standard XPath against bounded XML documents.</summary>
internal static class XmlXPathSelectorEvaluator
{
    public static string? EvaluateFirst(
        string documentBody,
        string expression,
        string? attributeName)
    {
        try
        {
            var document = Parse(documentBody);
            var iterator = document.CreateNavigator().Select(expression);
            if (iterator.MoveNext() && iterator.Current is { } current)
            {
                if (!string.IsNullOrWhiteSpace(attributeName))
                {
                    return current.NodeType == XPathNodeType.Element
                        ? current.GetAttribute(attributeName.Trim(), string.Empty)
                        : null;
                }

                return current.Value.Trim();
            }
        }
        catch (XmlException)
        {
        }
        catch (XPathException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }

        return null;
    }

    public static IReadOnlyList<SelectorElementSnapshot>? SelectAll(
        string documentBody,
        string expression)
    {
        try
        {
            var document = Parse(documentBody);
            var iterator = document.CreateNavigator().Select(expression);
            var result = new List<SelectorElementSnapshot>();
            while (iterator.MoveNext())
            {
                if (iterator.Current is not { } current)
                {
                    continue;
                }

                if (result.Count >= SelectorEvaluationLimits.MaxMatches)
                {
                    return [];
                }

                result.Add(ToSnapshot(current));
            }

            return result.Count > 0 ? result : null;
        }
        catch (XmlException)
        {
        }
        catch (XPathException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }

        return null;
    }

    private static XPathDocument Parse(string documentBody)
    {
        if (documentBody.Length > SelectorEvaluationLimits.MaxDocumentBytes)
        {
            throw new XmlException("selector document exceeds the size limit.");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = SelectorEvaluationLimits.MaxDocumentBytes,
            MaxCharactersFromEntities = 0,
            XmlResolver = null,
        };
        using var reader = XmlReader.Create(new StringReader(documentBody), settings);
        return new XPathDocument(reader);
    }

    private static SelectorElementSnapshot ToSnapshot(XPathNavigator current)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var attributeCursor = current.Clone();
        if (attributeCursor.NodeType == XPathNodeType.Element &&
            attributeCursor.MoveToFirstAttribute())
        {
            do
            {
                attributes[attributeCursor.Name] = attributeCursor.Value;
            }
            while (attributeCursor.MoveToNextAttribute());
        }

        return new(current.Value.Trim(), attributes);
    }
}
