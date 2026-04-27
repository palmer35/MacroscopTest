using System.Windows.Media.Imaging;

namespace MacroscopTest.Services;

public sealed class DownloadedImage
{
    public DownloadedImage(BitmapImage previewImage, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(previewImage);
        ArgumentNullException.ThrowIfNull(bytes);

        PreviewImage = previewImage;
        Bytes = bytes;
    }

    public BitmapImage PreviewImage { get; }

    public byte[] Bytes { get; }
}
