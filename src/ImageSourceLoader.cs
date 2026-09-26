using System.Net.Http;

namespace Ziopuzzle.CustomButton;

/// <summary>Read bounded image bytes on the plugin machine; clients receive a registered resource.</summary>
public static class ImageSourceLoader
{
    public sealed record Image(byte[] Bytes, string MediaType)
    {
        public double AspectRatio => ImageDimensions.AspectRatio(Bytes, MediaType);
    }
    public static async Task<Image> LoadAsync(string source, HttpClient http, int maxBytes, CancellationToken cancellationToken, Uri? hostUrl = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        source = source.Trim();
        if (string.IsNullOrEmpty(source)) throw new FormatException("Choose an image file or an HTTP/HTTPS URL.");
        if (source.StartsWith("icon-pack:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(source[10..], out var iconId)) throw new FormatException("Use icon-pack: followed by the icon's UUID from the standard button settings.");
            source = "/api/icons/" + iconId.ToString("D") + "/image";
        }
        // Resolve reserved host API paths before filesystem paths (on Unix these are absolute paths).
        if (source.StartsWith("/api/", StringComparison.Ordinal))
        {
            if (hostUrl == null || !hostUrl.IsAbsoluteUri || hostUrl.Scheme is not ("http" or "https"))
                throw new FormatException("The Macro Deck host URL is unavailable for this image source.");
            source = new Uri(hostUrl, source).AbsoluteUri;
        }
        if (Path.IsPathFullyQualified(source))
        {
            await using var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 8192, true);
            if (file.Length > maxBytes) throw new FormatException($"Image exceeds the {maxBytes}-byte limit.");
            return await ReadAsync(file, maxBytes, cancellationToken);
        }
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new FormatException("Use an absolute image file path or an HTTP/HTTPS URL.");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.CacheControl = new() { NoCache = true };
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes) throw new FormatException($"Image exceeds the {maxBytes}-byte limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await ReadAsync(stream, maxBytes, cancellationToken);
    }
    private static async Task<Image> ReadAsync(Stream source, int maxBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, maxBytes - output.Length + 1)), cancellationToken)) > 0)
        {
            if (output.Length + count > maxBytes) throw new FormatException($"Image exceeds the {maxBytes}-byte limit.");
            output.Write(buffer, 0, count);
        }
        var bytes = output.ToArray();
        var data = bytes.AsSpan();
        var type = data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png"
            : data.StartsWith(new byte[] { 255, 216, 255 }) ? "image/jpeg"
            : data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8) ? "image/gif"
            : data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8) ? "image/webp"
            : throw new FormatException("Use a PNG, JPEG, WebP or GIF image. SVG cannot be registered directly.");
        return new(bytes, type);
    }
}
