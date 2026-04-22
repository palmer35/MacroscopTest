using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace MacroscopTest.Services;

public class ImageDownloadService
{
    private const int MaxDecodePixelWidth = 2000;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public virtual async Task<BitmapImage> DownloadAsync(string url, CancellationToken cancellationToken)
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

        if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true)
        {
            throw new InvalidOperationException("URL does not point to an image.");
        }

        await using var responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var imageStream = response.Content.Headers.ContentLength is { } contentLength and > 0 and <= int.MaxValue
            ? new MemoryStream((int)contentLength)
            : new MemoryStream();

        await responseStream.CopyToAsync(imageStream, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        imageStream.Position = 0;

        return CreateBitmapImage(imageStream);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        return client;
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

    private static BitmapImage CreateBitmapImage(Stream imageStream)
    {
        try
        {
            BitmapImage image = new();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = MaxDecodePixelWidth;
            image.StreamSource = imageStream;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException)
        {
            throw new InvalidOperationException("The downloaded content is not a valid image.", exception);
        }
    }
}
