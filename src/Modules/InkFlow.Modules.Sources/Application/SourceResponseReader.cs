using System.Net.Http;

namespace InkFlow.Modules.Sources.Application;

/// <summary>在解码或解析前按来源执行预算读取响应体。</summary>
public static class SourceResponseReader
{
    public static async Task<byte[]> ReadBoundedBytesAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (content.Headers.ContentLength is { } contentLength && contentLength > maxBytes)
        {
            throw new SourceResponseTooLargeException();
        }

        using var stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream(Math.Min(maxBytes, 81_920));
        var chunk = new byte[Math.Min(maxBytes, 81_920)];
        var totalBytes = 0;

        while (true)
        {
            var read = await stream
                .ReadAsync(chunk.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (read > maxBytes - totalBytes)
            {
                throw new SourceResponseTooLargeException();
            }

            buffer.Write(chunk, 0, read);
            totalBytes += read;
        }

        return buffer.ToArray();
    }
}
