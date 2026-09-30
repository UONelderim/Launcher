namespace Nelderim.Utility;

public static class HttpClientExt
{
    //Progress reports value between 0 and 1
    public static async Task DownloadDataAsync(this HttpClient client,
        string requestUrl,
        Stream destination,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var contentLength = response.Content.Headers.ContentLength;

        await using var fileStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (progress != null && contentLength != null)
        {
            await fileStream.CopyToAsync(destination, contentLength.Value, progress, cancellationToken);
        }
        else
        {
            await fileStream.CopyToAsync(destination, cancellationToken);
        }
    }

    private static async Task CopyToAsync(this Stream source,
        Stream destination,
        long totalLength,
        IProgress<float> progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long totalBytesRead = 0;
        do
        {
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0) break;

            await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report((float)totalBytesRead / totalLength);
        } while (true);
    }
}