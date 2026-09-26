using System.Buffers.Binary;

namespace Ziopuzzle.CustomButton;

/// <summary>Reads canvas dimensions without decoding or changing the registered image bytes.</summary>
public static class ImageDimensions
{
    public static double AspectRatio(ReadOnlySpan<byte> bytes, string mediaType)
    {
        static double Ratio(uint width, uint height) => width == 0 || height == 0 ? 1 : (double)width / height;
        if (mediaType == "image/png" && bytes.Length >= 24 && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            return Ratio(BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]));
        if (mediaType == "image/gif" && bytes.Length >= 10)
            return Ratio(BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]));
        if (mediaType == "image/webp")
        {
            for (var offset = 12; offset <= bytes.Length - 8;)
            {
                var kind = bytes.Slice(offset, 4);
                var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
                if (length > bytes.Length - offset - 8) break;
                var data = bytes.Slice(offset + 8, (int)length);
                if (kind.SequenceEqual("VP8X"u8) && data.Length >= 10)
                    return Ratio(1u + U24(data[4..]), 1u + U24(data[7..]));
                if (kind.SequenceEqual("VP8L"u8) && data.Length >= 5 && data[0] == 0x2f)
                {
                    var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[1..]);
                    return Ratio(1 + (bits & 0x3fff), 1 + ((bits >> 14) & 0x3fff));
                }
                if (kind.SequenceEqual("VP8 "u8) && data.Length >= 10 && data.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 0x01, 0x2a }))
                    return Ratio((uint)(BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) & 0x3fff), (uint)(BinaryPrimitives.ReadUInt16LittleEndian(data[8..]) & 0x3fff));
                offset = checked(offset + 8 + (int)length + (int)(length & 1));
            }
        }
        if (mediaType == "image/jpeg")
        {
            for (var offset = 2; offset < bytes.Length;)
            {
                if (bytes[offset++] != 0xff) break;
                while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
                if (offset >= bytes.Length) break;
                var marker = bytes[offset++];
                if (marker is 0xd9 or 0xda) break;
                if (marker is 0x01 or >= 0xd0 and <= 0xd8) continue;
                if (offset > bytes.Length - 2) break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
                if (length < 2 || length > bytes.Length - offset) break;
                if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc) && length >= 8)
                    return Ratio(BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 5)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset + 3)..]));
                offset += length;
            }
        }
        // The host remains responsible for validating/decoding image contents.
        return 1;
    }
    private static uint U24(ReadOnlySpan<byte> value) => (uint)(value[0] | value[1] << 8 | value[2] << 16);
}
