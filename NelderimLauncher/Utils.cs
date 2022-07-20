using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NelderimLauncher;

public static class Utils
{
    public static string Sha1Hash(Stream stream) {
        using (SHA1 sha1 = SHA1.Create()) {
            byte[] hash = sha1.ComputeHash(stream);
            StringBuilder sb = new StringBuilder(hash.Length * 2);

            foreach (byte b in hash) {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
    
    public static async Task DownloadDataAsync (this HttpClient client, string requestUrl, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
		{
			using (var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead)) {
				var contentLength = response.Content.Headers.ContentLength;
				using (var fileStream = await response.Content.ReadAsStreamAsync ()) {
					if (progress is null || !contentLength.HasValue) {
						await fileStream.CopyToAsync (destination);
						return;
					}
					var progressWrapper = new Progress<long> (totalBytes => progress.Report (GetProgressPercentage (totalBytes, contentLength.Value)));
					await fileStream.CopyToAsync (destination, 81920, progressWrapper, cancellationToken);
				}
			}

			float GetProgressPercentage (float totalBytes, float currentBytes) => (totalBytes / currentBytes) * 100f;
		}

		static async Task CopyToAsync (this Stream source, Stream destination, int bufferSize, IProgress<long> progress = null, CancellationToken cancellationToken = default)
		{
			if (bufferSize < 0)
				throw new ArgumentOutOfRangeException (nameof (bufferSize));
			if (source is null)
				throw new ArgumentNullException (nameof (source));
			if (!source.CanRead)
				throw new InvalidOperationException ($"'{nameof (source)}' is not readable.");
			if (destination == null)
				throw new ArgumentNullException (nameof (destination));
			if (!destination.CanWrite)
				throw new InvalidOperationException ($"'{nameof (destination)}' is not writable.");

			var buffer = new byte[bufferSize];
			long totalBytesRead = 0;
			int bytesRead;
			while ((bytesRead = await source.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) != 0) {
				await destination.WriteAsync (buffer, 0, bytesRead, cancellationToken).ConfigureAwait (false);
				totalBytesRead += bytesRead;
				progress?.Report (totalBytesRead);
			}
		}
}