using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace MacroscopTest.Services;

public class ImageDownloadService
{
    private const int BufferSize = 81920;
    private const int PreviewDecodePixelWidth = 1600;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public virtual async Task<DownloadedImage> DownloadAsync(
        string url,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        var uri = CreateImageUri(url);

        using var response = await HttpClient.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Image request failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (mediaType is not null &&
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("URL does not point to an image.");
        }

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        var contentLength = response.Content.Headers.ContentLength;
        await using var imageStream = CreateMemoryStream(contentLength);

        await CopyToMemoryAsync(
            responseStream,
            imageStream,
            contentLength,
            progress,
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        imageStream.Position = 0;
        progress?.Report(100);

        var bytes = imageStream.ToArray();
        var previewImage = CreateBitmapImage(bytes, PreviewDecodePixelWidth);

        return new DownloadedImage(previewImage, bytes);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        return client;
    }

    private static MemoryStream CreateMemoryStream(long? contentLength)
    {
        return contentLength is > 0 and <= int.MaxValue
            ? new MemoryStream((int)contentLength.Value)
            : new MemoryStream();
    }

    private static async Task CopyToMemoryAsync(
        Stream source,
        Stream destination,
        long? contentLength,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long downloadedBytes = 0;

        try
        {
            while (true)
            {
                var bytesRead = await source
                    .ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    return;
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);

                downloadedBytes += bytesRead;
                ReportDownloadProgress(progress, downloadedBytes, contentLength);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ReportDownloadProgress(
        IProgress<double>? progress,
        long downloadedBytes,
        long? contentLength)
    {
        if (progress is null || contentLength is not > 0)
        {
            return;
        }

        var percent = Math.Min(100, downloadedBytes * 100d / contentLength.Value);

        progress.Report(percent);
    }

    private static Uri CreateImageUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Image URL cannot be empty.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Image URL must be a valid absolute HTTP or HTTPS address.", nameof(url));
        }

        return uri;
    }

    public static BitmapImage CreateBitmapImage(byte[] bytes, int? decodePixelWidth = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            using var imageStream = new MemoryStream(bytes);
            BitmapImage image = new();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth is > 0)
            {
                image.DecodePixelWidth = decodePixelWidth.Value;
            }

            image.StreamSource = imageStream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException)
        {
            throw new InvalidOperationException("The downloaded content is not a valid image.", exception);
        }
    }
}
