using Districauchos.Sheets;
using SkiaSharp;
using Xunit;

namespace Districauchos.Core.Tests;

public sealed class DrivePhotoSyncTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dc-drive-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    /// <summary>JPG de 600×300 con fondo blanco y un rectángulo negro de 100×50 descentrado.</summary>
    internal static byte[] Jpg(SKColor? color = null)
    {
        using var bmp = new SKBitmap(600, 300);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            c.DrawRect(SKRect.Create(50, 40, 100, 50), new SKPaint { Color = color ?? SKColors.Black });
        }
        using var img = SKImage.FromBitmap(bmp);
        return img.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray();
    }

    private sealed class FakeDrive : IDrivePhotoSource
    {
        public List<DrivePhotoFile> Files { get; } = [];
        public Dictionary<string, byte[]> Content { get; } = [];
        public int Downloads;
        public Task<IReadOnlyList<DrivePhotoFile>> ListAsync(string folderId, CancellationToken ct) => Task.FromResult<IReadOnlyList<DrivePhotoFile>>(Files.ToList());
        public Task<byte[]> DownloadAsync(string fileId, CancellationToken ct) { Downloads++; return Task.FromResult(Content[fileId]); }
        public void Put(string id, string name, string md5, byte[] bytes, int minute = 0)
        {
            Files.RemoveAll(f => f.Id == id);
            Files.Add(new DrivePhotoFile(id, name, md5, new DateTimeOffset(2026, 11, 21, 10, minute, 0, TimeSpan.Zero), bytes.Length));
            Content[id] = bytes;
        }
    }

    private string Photo(string key) => Path.Combine(_dir, DrivePhotoSync.PhotosSubdir, key + ".webp");

    [Fact]
    public void Process_trims_and_centers_on_white_square()
    {
        var webp = PhotoProcessor.Process(Jpg())!;
        using var bmp = SKBitmap.Decode(webp);
        Assert.Equal(1000, bmp.Width);
        Assert.Equal(1000, bmp.Height);
        Assert.True(bmp.GetPixel(500, 500).Red < 40);   // la pieza quedó al centro
        Assert.True(bmp.GetPixel(500, 100).Red > 240);  // margen blanco arriba (100×50 → 880×440)
        Assert.True(bmp.GetPixel(30, 500).Red > 240);   // margen de 60 px a los lados
        Assert.True(bmp.GetPixel(80, 500).Red < 40);
    }

    [Fact]
    public void Process_returns_null_for_non_images() => Assert.Null(PhotoProcessor.Process("no es foto"u8.ToArray()));

    [Fact]
    public async Task Downloads_only_new_or_changed_and_removes_deleted()
    {
        var drive = new FakeDrive();
        drive.Put("1", "8'94408'840'1.jpg", "a", Jpg());
        drive.Put("2", "DH-40.png", "b", Jpg());
        var sync = new DrivePhotoSync(drive, _dir);

        var r1 = await sync.RunAsync("carpeta", default);
        Assert.Equal((2, 0, 0, 2), (r1.Added, r1.Updated, r1.Removed, r1.Total));
        Assert.True(File.Exists(Photo("8944088401")));
        Assert.True(File.Exists(Photo("DH40")));

        var r2 = await sync.RunAsync("carpeta", default);   // nada cambió: no baja nada
        Assert.False(r2.Changed);
        Assert.Equal(2, drive.Downloads);

        drive.Put("2", "DH-40.png", "b2", Jpg(SKColors.Red)); // reemplazada en Drive
        drive.Files.RemoveAll(f => f.Id == "1");             // borrada en Drive
        var r3 = await sync.RunAsync("carpeta", default);
        Assert.Equal((0, 1, 1, 1), (r3.Added, r3.Updated, r3.Removed, r3.Total));
        Assert.False(File.Exists(Photo("8944088401")));
        Assert.Equal(3, drive.Downloads);
    }

    [Fact]
    public async Task Same_key_keeps_newest_and_bad_files_are_reported_once()
    {
        var drive = new FakeDrive();
        drive.Put("1", "93378096.jpg", "a", Jpg(), minute: 1);
        drive.Put("2", "93378096 (1).jpg", "b", Jpg(), minute: 5); // "(1)" de Drive: clave 933780961, otra foto
        drive.Put("3", "93-378-096.jpg", "c", Jpg(SKColors.Red), minute: 9);
        drive.Put("4", "IMG.heic", "d", "xx"u8.ToArray());
        var sync = new DrivePhotoSync(drive, _dir);

        var r = await sync.RunAsync("carpeta", default);
        Assert.Contains(r.Problems, p => p.Contains("misma foto") && p.Contains("93-378-096.jpg"));
        Assert.Contains(r.Problems, p => p.StartsWith("IMG.heic"));
        using (var bmp = SKBitmap.Decode(File.ReadAllBytes(Photo("93378096"))))
            Assert.True(bmp.GetPixel(500, 500).Green < 60); // ganó la roja (la más nueva)

        var downloads = drive.Downloads;
        var again = await sync.RunAsync("carpeta", default);
        Assert.Equal(downloads, drive.Downloads);            // la HEIC no se vuelve a bajar cada 5 min
        Assert.DoesNotContain(again.Problems, p => p.StartsWith("IMG.heic"));
    }
}
