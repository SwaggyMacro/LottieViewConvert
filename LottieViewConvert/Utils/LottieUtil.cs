using System.IO;

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
        return new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
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