namespace InkFlow.Modules.Identity.Application;

public sealed record AvatarImage(string ContentType, byte[] Content)
{
    public const int MaxUploadBytes = 2 * 1024 * 1024;
    public const int MaxMultipartRequestBytes = MaxUploadBytes + (128 * 1024);

    private static readonly byte[] PngSignature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static async Task<AvatarImage?> ReadAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (!content.CanRead)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[80 * 1024];
        while (true)
        {
            var read = await content
                .ReadAsync(chunk.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaxUploadBytes)
            {
                return null;
            }

            await buffer
                .WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        var bytes = buffer.ToArray();
        var contentType = DetectContentType(bytes);
        return contentType is null ? null : new AvatarImage(contentType, bytes);
    }

    private static string? DetectContentType(ReadOnlySpan<byte> content) =>
        content.Length >= PngSignature.Length &&
        content[..PngSignature.Length].SequenceEqual(PngSignature)
            ? "image/png"
            : content.Length >= 3 &&
              content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF
                ? "image/jpeg"
                : content.Length >= 12 &&
                  content[..4].SequenceEqual("RIFF"u8) &&
                  content[8..12].SequenceEqual("WEBP"u8)
                    ? "image/webp"
                    : null;
}
