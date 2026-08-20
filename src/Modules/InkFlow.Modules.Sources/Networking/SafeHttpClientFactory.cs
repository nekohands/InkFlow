using System.Net;
using System.Net.Sockets;

namespace InkFlow.Modules.Sources.Networking;

public static class SafeHttpClientFactory
{
    public static HttpClient Create(SafeEndpointValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            var scheme = context.InitialRequestMessage?.RequestUri?.Scheme ?? Uri.UriSchemeHttps;
            var endpointUri = new UriBuilder(scheme, context.DnsEndPoint.Host, context.DnsEndPoint.Port).Uri;
            var validated = await validator.ValidateAsync(endpointUri, cancellationToken).ConfigureAwait(false);

            Exception? lastException = null;
            foreach (var address in validated.Addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                        .ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                {
                    socket.Dispose();
                    lastException = exception;
                    if (exception is OperationCanceledException)
                    {
                        throw;
                    }
                }
            }

            throw new HttpRequestException(
                $"Unable to connect to validated host '{context.DnsEndPoint.Host}'.",
                lastException);
        };

        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}
