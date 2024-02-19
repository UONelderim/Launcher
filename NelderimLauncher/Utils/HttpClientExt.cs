namespace Nelderim.Utility;

public static class HttpClientExt
{
    //Progress reports value between 0 and 1
    public static async Task DownloadDataAsync(this HttpClient client,
        string requestUrl,
        Stream destination,
        IProgress<float>? progress = null)
    {
        using var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
        var contentLength = response.Content.Headers.ContentLength;

        await using var fileStream = await response.Content.ReadAsStreamAsync();
        if (progress != null && contentLength != null)
        {
            await fileStream.CopyToAsync(destination, contentLength.Value, progress);
        }
        else
        {
            await fileStream.CopyToAsync(destination);
        }
    }

    private static async Task CopyToAsync(this Stream source,
        Stream destination,
        long totalLength,
        IProgress<float> progress)
    {
        var buffer = new byte[64 * 1024];
        long totalBytesRead = 0;
        do
        {
            var bytesRead = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (bytesRead == 0) break;

            await destination.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report((float)totalBytesRead / totalLength);
        } while (true);
    }
}