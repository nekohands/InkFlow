using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkFlow.ReaderFixtures;

var output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/reader-fixtures");
Directory.CreateDirectory(output);
var manifest = new List<object>();
foreach (var (name, page) in ReaderFixture.Create())
{
    // Git checkouts use platform line endings; compare canonical LF output across OSes.
    var body = page.Body.Replace("\r\n", "\n", StringComparison.Ordinal);
    var bytes = Encoding.UTF8.GetBytes(body);
    var file = name + ".txt";
    await File.WriteAllBytesAsync(Path.Combine(output, file), bytes);
    manifest.Add(new { name, page.Path, page.ContentType, page.Status, file, bytes = bytes.Length, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
}
await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Exported {manifest.Count} deterministic Reader cases to {output}");
