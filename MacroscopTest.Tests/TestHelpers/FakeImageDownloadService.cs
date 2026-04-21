using System.IO;
using System.Windows.Media.Imaging;
using MacroscopTest.Services;

namespace MacroscopTest.Tests.TestHelpers;

internal sealed class FakeImageDownloadService : ImageDownloadService
{
    private static readonly byte[] OnePixelPng =
    {
        137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
        0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196,
        137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 248, 255, 255, 63,
        0, 5, 254, 2, 254, 167, 53, 129, 132, 0, 0, 0, 0, 73, 69, 78,
        68, 174, 66, 96, 130
    };

    public Func<string, CancellationToken, Task<BitmapImage>>? OnDownloadAsync { get; set; }

    public override Task<BitmapImage> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (OnDownloadAsync is not null)
        {
            return OnDownloadAsync(url, cancellationToken);
        }

        return Task.FromResult(CreateImage());
    }

    public static BitmapImage CreateImage()
    {
        using var stream = new MemoryStream(OnePixelPng);
        var image = new BitmapImage();

        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();

        return image;
    }
}
