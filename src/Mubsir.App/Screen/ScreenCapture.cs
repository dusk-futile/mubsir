using Mubsir.App.Screen.Interop;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mubsir.App.Screen;

internal sealed record CapturedImage(byte[] Pixels, int Width, int Height);

internal static unsafe class ScreenCapture
{
    public const int MaxCaptureSide = 8192;

    public static CapturedImage? CaptureWindow(nint hwnd)
    {
        if (hwnd == 0 || !ScreenNative.IsWindow(hwnd) || ScreenNative.IsIconic(hwnd))
        {
            return null;
        }

        if (!ScreenNative.TryGetFrameBounds(hwnd, out var r))
        {
            return null;
        }

        int vx = ScreenNative.GetSystemMetrics(ScreenNative.SmXVirtualScreen);
        int vy = ScreenNative.GetSystemMetrics(ScreenNative.SmYVirtualScreen);
        int vw = ScreenNative.GetSystemMetrics(ScreenNative.SmCxVirtualScreen);
        int vh = ScreenNative.GetSystemMetrics(ScreenNative.SmCyVirtualScreen);
        int left = Math.Max(r.Left, vx);
        int top = Math.Max(r.Top, vy);
        int right = vw > 0 ? Math.Min(r.Right, vx + vw) : r.Right;
        int bottom = vh > 0 ? Math.Min(r.Bottom, vy + vh) : r.Bottom;
        int width = Math.Min(right - left, MaxCaptureSide);
        int height = Math.Min(bottom - top, MaxCaptureSide);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        nint screenDc = ScreenNative.GetDC(0);
        if (screenDc == 0)
        {
            return null;
        }

        nint memDc = 0;
        nint bitmap = 0;
        nint old = 0;
        try
        {
            memDc = ScreenNative.CreateCompatibleDC(screenDc);
            if (memDc == 0)
            {
                return null;
            }

            var header = new ScreenNative.BitmapInfoHeader
            {
                Size = (uint)sizeof(ScreenNative.BitmapInfoHeader),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
            };

            bitmap = ScreenNative.CreateDIBSection(memDc, &header, ScreenNative.DibRgbColors, out void* bits, 0, 0);
            if (bitmap == 0 || bits is null)
            {
                return null;
            }

            old = ScreenNative.SelectObject(memDc, bitmap);
            if (!ScreenNative.BitBlt(memDc, 0, 0, width, height, screenDc, left, top, ScreenNative.Srccopy | ScreenNative.Captureblt))
            {
                return null;
            }

            ScreenNative.GdiFlush();
            var pixels = new byte[width * height * 4];
            new ReadOnlySpan<byte>(bits, pixels.Length).CopyTo(pixels);

            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }

            return new CapturedImage(pixels, width, height);
        }
        finally
        {
            if (old != 0)
            {
                ScreenNative.SelectObject(memDc, old);
            }

            if (bitmap != 0)
            {
                ScreenNative.DeleteObject(bitmap);
            }

            if (memDc != 0)
            {
                ScreenNative.DeleteDC(memDc);
            }

            ScreenNative.ReleaseDC(0, screenDc);
        }
    }
}

internal static class ImageCodec
{
    public static (uint Width, uint Height) Fit(int width, int height, int maxSide)
    {
        if (width <= 0 || height <= 0)
        {
            return (0, 0);
        }

        int longSide = Math.Max(width, height);
        if (maxSide <= 0 || longSide <= maxSide)
        {
            return ((uint)width, (uint)height);
        }

        double scale = (double)maxSide / longSide;
        return ((uint)Math.Max(1, (int)Math.Round(width * scale)), (uint)Math.Max(1, (int)Math.Round(height * scale)));
    }

    public static async Task<byte[]> EncodeJpegAsync(CapturedImage image, int maxLongSide, float quality, CancellationToken ct)
    {
        using var stream = new InMemoryRandomAccessStream();
        var options = new BitmapPropertySet
        {
            ["ImageQuality"] = new BitmapTypedValue(quality, PropertyType.Single),
        };

        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options).AsTask(ct).ConfigureAwait(false);
        await WritePixelsAsync(encoder, image, Fit(image.Width, image.Height, maxLongSide), ct).ConfigureAwait(false);
        return await ReadAllAsync(stream, ct).ConfigureAwait(false);
    }

    public static (uint Width, uint Height) OcrSize(int width, int height, int maxSide)
    {
        if (width <= 0 || height <= 0 || maxSide <= 0)
        {
            return (0, 0);
        }

        double scale = Math.Min(2.0, (double)maxSide / Math.Max(width, height));
        return ((uint)Math.Max(1, (int)Math.Floor(width * scale)), (uint)Math.Max(1, (int)Math.Floor(height * scale)));
    }

    public static async Task<SoftwareBitmap> ToOcrBitmapAsync(CapturedImage image, int maxSide, CancellationToken ct)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream).AsTask(ct).ConfigureAwait(false);
        await WritePixelsAsync(encoder, image, OcrSize(image.Width, image.Height, maxSide), ct).ConfigureAwait(false);
        stream.Seek(0);
        return await DecodeAsync(stream, maxSide, ct).ConfigureAwait(false);
    }

    public static async Task<SoftwareBitmap> DecodeAsync(IRandomAccessStream stream, int maxSide, CancellationToken ct)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);
        var (w, h) = Fit((int)decoder.PixelWidth, (int)decoder.PixelHeight, maxSide);
        var transform = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Fant };
        if (w != decoder.PixelWidth || h != decoder.PixelHeight)
        {
            transform.ScaledWidth = w;
            transform.ScaledHeight = h;
        }

        return await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage)
            .AsTask(ct)
            .ConfigureAwait(false);
    }

    private static async Task WritePixelsAsync(BitmapEncoder encoder, CapturedImage image, (uint Width, uint Height) target, CancellationToken ct)
    {
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)image.Width,
            (uint)image.Height,
            96,
            96,
            image.Pixels);

        var (w, h) = target;
        if (w > 0 && h > 0 && (w != image.Width || h != image.Height))
        {
            encoder.BitmapTransform.ScaledWidth = w;
            encoder.BitmapTransform.ScaledHeight = h;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadAllAsync(IRandomAccessStream stream, CancellationToken ct)
    {
        ulong size = stream.Size;
        if (size == 0 || size > int.MaxValue)
        {
            return [];
        }

        var bytes = new byte[(int)size];
        using var input = stream.GetInputStreamAt(0);
        using var reader = new DataReader(input);
        uint loaded = await reader.LoadAsync((uint)size).AsTask(ct).ConfigureAwait(false);
        if (loaded < size)
        {
            Array.Resize(ref bytes, (int)loaded);
        }

        reader.ReadBytes(bytes);
        return bytes;
    }
}
