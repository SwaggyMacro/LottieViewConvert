using System;
using System.IO;
using System.IO.Compression;

namespace LottieViewConvert.Utils;

public class LottieUtil
{
    /// <summary>
    /// Checks if the provided stream is Gzip compressed.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static bool IsGzipCompressed(Stream stream)
    {
        if (!stream.CanSeek)
            stream = new BufferedStream(stream);

        var header = new byte[2];
        var read = stream.Read(header, 0, 2);
        stream.Seek(-read, SeekOrigin.Current);

        // gzip magic number
        return read == 2 && header[0] == 0x1F && header[1] == 0x8B;
    }

    /// <summary>
    /// Uncompresses a Gzip compressed stream.
    /// </summary>
    /// <param name="compressedStream"></param>
    /// <returns></returns>
    public static Stream UncompressGzip(Stream compressedStream)
    {
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress, leaveOpen: false);
        var ms = new MemoryStream();
        gzip.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Open a Lottie file, decompressing it if necessary.
    /// </summary>
    /// <param name="path">The path to the Lottie file.</param>
    /// <returns>A stream containing the Lottie JSON data.</returns>
    public static Stream OpenLottieStream(string path)
    {
        Stream rawStream;
        if (Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri)
            && uri is { IsAbsoluteUri: true, IsFile: true })
        {
            rawStream = File.OpenRead(uri.LocalPath);
        }
        else
        {
            rawStream = File.OpenRead(path);
        }

        if (!rawStream.CanSeek)
            rawStream = new BufferedStream(rawStream);

        Span<byte> header = stackalloc byte[2];
        var read = rawStream.Read(header);
        rawStream.Seek(-read, SeekOrigin.Current);

        if (read != 2 || header[0] != 0x1F || header[1] != 0x8B) return rawStream;

        using var gzip = new GZipStream(rawStream, CompressionMode.Decompress, leaveOpen: false);
        var ms = new MemoryStream();
        gzip.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return ms;
    }

    /// <summary>
    /// Validates if the provided JSON content is a valid Lottie JSON.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static bool IsValidLottieJson(string content)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(content);
            return json.RootElement.TryGetProperty("v", out _) &&
                   json.RootElement.TryGetProperty("fr", out _) &&
                   json.RootElement.TryGetProperty("ip", out _) &&
                   json.RootElement.TryGetProperty("op", out _);
        }
        catch
        {
            return false;
        }
    }
}