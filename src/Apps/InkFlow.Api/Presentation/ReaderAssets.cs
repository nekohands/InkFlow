namespace InkFlow.Api.Presentation;

// Assets are embedded in the API assembly; publication never needs the source tree.
internal static class ReaderAssets
{
    internal static readonly string ReaderClientScript = Script("reader-client.js");
    internal static readonly string Head = Read("head.html").Replace("{{READER_CSS}}", Read("reader.css"), StringComparison.Ordinal) + ReaderClientScript;
    internal static readonly string ReaderScript = Script("reader-settings.js");
    internal static readonly string ReaderHeader = Read("reader-header.html");
    internal static readonly string ReaderPwaScript = Script("reader-pwa.js");

    private static string Script(string name) => "<script>\n" + Read(name) + "\n</script>";

    private static string Read(string name)
    {
        using var stream = typeof(ReaderAssets).Assembly.GetManifestResourceStream("InkFlow.Api.Presentation.Assets." + name)
            ?? throw new InvalidOperationException($"Missing embedded Reader asset: {name}");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        // One canonical output across Windows/Linux; source files may have a final newline.
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }
}
