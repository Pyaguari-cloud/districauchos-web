using SkiaSharp;

namespace Districauchos.Sheets;

/// <summary>
/// Lo mismo que <c>web/scripts/fotos-productos.mjs</c>, en .NET para el Worker: endereza según EXIF, fondo blanco,
/// recorta el blanco sobrante, centra la pieza en 880 px dentro de 1000×1000 y guarda WebP calidad 82.
/// No borra fondos ni marcas de agua.
/// </summary>
public static class PhotoProcessor
{
    public const int Size = 1000, Inner = 880, Quality = 82;
    /// <summary>Un píxel cuenta como fondo si sus 3 canales están a menos de esto del blanco (como trim threshold 18).</summary>
    private const int Threshold = 18;
    /// <summary>Fotos de celular de 12 MP se decodifican reducidas para no gastar memoria (el Worker tiene 384 MB).</summary>
    private const int MaxDecode = 2400;

    /// <summary>Devuelve el WebP listo, o null si el archivo no es una imagen que se pueda leer (p. ej. HEIC).</summary>
    public static byte[]? Process(byte[] input)
    {
        using var source = Decode(input);
        if (source is null) return null;

        using var flat = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(flat))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(source, 0, 0);
        }

        var box = ContentBounds(flat) ?? new SKRectI(0, 0, flat.Width, flat.Height);
        var scale = Math.Min((float)Inner / box.Width, (float)Inner / box.Height);
        var w = box.Width * scale;
        var h = box.Height * scale;
        var dest = SKRect.Create((Size - w) / 2, (Size - h) / 2, w, h);

        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        using (var image = SKImage.FromBitmap(flat))
            surface.Canvas.DrawImage(image, box, dest, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Webp, Quality);
        return data?.ToArray();
    }

    /// <summary>Decodifica (reducida si es muy grande) y aplica la orientación EXIF.</summary>
    private static SKBitmap? Decode(byte[] input)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(input));
        if (codec is null) return null;

        var info = codec.Info;
        var max = Math.Max(info.Width, info.Height);
        if (max > MaxDecode)
        {
            var scaled = codec.GetScaledDimensions((float)MaxDecode / max);
            info = info.WithSize(scaled.Width, scaled.Height);
        }
        info = info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Premul);

        var raw = new SKBitmap(info);
        var result = codec.GetPixels(info, raw.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            // El códec no reduce este formato (PNG/WebP): se decodifica completa.
            raw.Dispose();
            raw = SKBitmap.Decode(input);
            if (raw is null) return null;
        }
        return Orient(raw, codec.EncodedOrigin);
    }

    private static SKBitmap Orient(SKBitmap bmp, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft) return bmp;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outBmp = new SKBitmap(swap ? bmp.Height : bmp.Width, swap ? bmp.Width : bmp.Height, bmp.ColorType, bmp.AlphaType);
        float w = bmp.Width, h = bmp.Height;
        // x' = ScaleX·x + SkewX·y + TransX ; y' = SkewY·x + ScaleY·y + TransY
        var m = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // espejo horizontal
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // espejo vertical
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transpuesta
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° horario (celular vertical)
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transversa
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° antihorario
            _ => SKMatrix.Identity,
        };
        using (var c = new SKCanvas(outBmp))
        {
            c.SetMatrix(m);
            c.DrawBitmap(bmp, 0, 0);
        }
        bmp.Dispose();
        return outBmp;
    }

    /// <summary>Rectángulo con todo lo que no es casi blanco; null si la foto es toda blanca.</summary>
    internal static SKRectI? ContentBounds(SKBitmap bmp)
    {
        var span = bmp.GetPixelSpan(); // RGBA, 4 bytes por píxel
        int w = bmp.Width, h = bmp.Height, stride = bmp.RowBytes;
        int left = w, top = h, right = -1, bottom = -1;
        const int limit = 255 - Threshold;
        for (var y = 0; y < h; y++)
        {
            var row = span.Slice(y * stride, w * 4);
            for (var x = 0; x < w; x++)
            {
                var p = x * 4;
                if (row[p] >= limit && row[p + 1] >= limit && row[p + 2] >= limit) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                bottom = y;
            }
        }
        return right < 0 ? null : new SKRectI(left, top, right + 1, bottom + 1);
    }
}
