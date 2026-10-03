using Microsoft.AspNetCore.WebUtilities;

namespace Ziopuzzle.CustomButton;

/// <summary>Recognizes host-owned images before any HTTP or filesystem access.</summary>
public sealed record HostImageSource(Guid? IconId, string? InstanceId, string? ArtworkId)
{
    public static HostImageSource? Parse(string source)
    {
        source = source.Trim();
        if (source.StartsWith("icon-pack:", StringComparison.OrdinalIgnoreCase))
            return Icon(source[10..]);
        var explicitArtwork = source.StartsWith("artwork:", StringComparison.Ordinal);
        if (explicitArtwork) source = source[8..];
        const string artworkPath = "/api/music-player/artwork/";
        const string iconPath = "/api/icons/";
        if (source.StartsWith(artworkPath, StringComparison.Ordinal))
        {
            var uri = new Uri(new Uri("http://host-image.invalid"), source);
            var artworkId = Uri.UnescapeDataString(uri.AbsolutePath[artworkPath.Length..]);
            var query = QueryHelpers.ParseQuery(uri.Query);
            var instanceId = query.TryGetValue("instanceId", out var instances) && instances.Count == 1 ? instances.ToString() : "";
            if (string.IsNullOrWhiteSpace(artworkId) || artworkId.Contains('/') || string.IsNullOrWhiteSpace(instanceId))
                throw new FormatException("Artwork requires an artwork ID and instanceId.");
            return new(null, instanceId, artworkId);
        }
        if (explicitArtwork) throw new FormatException("Use artwork:/api/music-player/artwork/ID?instanceId=PROVIDER%3A%3APLAYER.");
        if (source.StartsWith(iconPath, StringComparison.Ordinal))
        {
            var path = source.Split('?', '#')[0];
            if (!path.EndsWith("/image", StringComparison.Ordinal)) return null;
            return Icon(path[iconPath.Length..^6]);
        }
        return null;
    }
    private static HostImageSource Icon(string id)
        => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty ? new(parsed, null, null)
            : throw new FormatException("Use the installed icon's UUID for an Icon Pack image.");
}
