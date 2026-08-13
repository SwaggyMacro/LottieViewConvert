using System.IO.Compression;
using Avalonia.Platform;

namespace Lottie;

public static class LottieFile
{
    public static Stream OpenFile(string path)
    {
        var filePath = Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile
            ? uri.LocalPath
            : path;

        return Decode(File.OpenRead(filePath));
    }

    public static Stream Open(string path, Uri contextBase)
    {
        Stream stream;
        if (Path.IsPathRooted(path))
        {
            stream = File.OpenRead(path);
        }
        else
        {
            var uri = new Uri(path, UriKind.RelativeOrAbsolute);
            stream = uri is { IsAbsoluteUri: true, IsFile: true }
                ? File.OpenRead(uri.LocalPath)
                : AssetLoader.Open(uri, contextBase);
        }

        return Decode(stream);
    }

    private static Stream Decode(Stream stream)
    {
        if (!stream.CanSeek)
        {
            var seekable = new MemoryStream();
            stream.CopyTo(seekable);
            stream.Dispose();
            seekable.Position = 0;
            stream = seekable;
        }

        Span<byte> header = stackalloc byte[4];
        var read = stream.Read(header);
        stream.Position = 0;

        if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
        {
            using (stream)
            using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            {
                return CopyToMemory(gzip);
            }
        }

        if (read == 4 && header[0] == 0x50 && header[1] == 0x4B &&
            header[2] is 0x03 or 0x05 or 0x07 && header[3] is 0x04 or 0x06 or 0x08)
        {
            using (stream)
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var animationEntry = archive.Entries
                    .Where(entry => entry.FullName.StartsWith("animations/", StringComparison.OrdinalIgnoreCase) &&
                                    entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.FullName, StringComparer.Ordinal)
                    .FirstOrDefault()
                    ?? archive.Entries.FirstOrDefault(entry =>
                        entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

                if (animationEntry == null)
                {
                    throw new InvalidDataException("The .lottie archive contains no animation JSON.");
                }

                using var animationStream = animationEntry.Open();
                return CopyToMemory(animationStream);
            }
        }

        return stream;
    }

    private static MemoryStream CopyToMemory(Stream source)
    {
        var memory = new MemoryStream();
        source.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }
}
