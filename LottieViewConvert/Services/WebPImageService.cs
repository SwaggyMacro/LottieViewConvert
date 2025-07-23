using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace LottieViewConvert.Services
{
    public class WebPImageService
    {
        private static readonly Lazy<WebPImageService> _instance = new(() => new WebPImageService());
        public static WebPImageService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
        private readonly SemaphoreSlim _semaphore = new(Environment.ProcessorCount); // limit concurrent access

        private WebPImageService() { }

        public async Task<Bitmap?> LoadImageAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // check cache first
            if (_cache.TryGetValue(filePath, out var cachedBitmap))
                return cachedBitmap;

            // limit concurrent access to the cache
            await _semaphore.WaitAsync();
            
            try
            {
                // double-check cache after acquiring the semaphore
                if (_cache.TryGetValue(filePath, out cachedBitmap))
                    return cachedBitmap;

                var bitmap = await Task.Run(() => LoadImageInternal(filePath));
                _cache.TryAdd(filePath, bitmap);
                return bitmap;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private Bitmap? LoadImageInternal(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".webp")
                {
                    // WebP decoding
                    using var codec = SKCodec.Create(filePath);
                    if (codec != null)
                    {
                        var info = codec.Info;
                        
                        // if the image is too large, scale it down
                        var maxSize = 512; // max size for preview
                        var scale = Math.Min(1.0f, Math.Min((float)maxSize / info.Width, (float)maxSize / info.Height));
                        
                        var scaledWidth = (int)(info.Width * scale);
                        var scaledHeight = (int)(info.Height * scale);

                        using var bitmap = new SKBitmap(scaledWidth, scaledHeight);
                        var scaledInfo = new SKImageInfo(scaledWidth, scaledHeight);
                        
                        var result = codec.GetPixels(scaledInfo, bitmap.GetPixels());
                        
                        if (result == SKCodecResult.Success)
                        {
                            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 80); // lower quality for saving memory
                            using var stream = new MemoryStream(data.ToArray());
                            return new Bitmap(stream);
                        }
                    }
                }
                else
                {
                    // regular image loading via memory stream to allow file deletion after load
                    using var fs = File.OpenRead(filePath);
                    var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    return new Bitmap(ms);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image {filePath}: {ex.Message}");
            }
            
            return null;
        }

        public void ClearCache()
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap?.Dispose();
            }
            _cache.Clear();
        }
    }
}