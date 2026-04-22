using System.Text;

namespace OpenGen;

internal static class InspectLinkParser
{
    private const string ConsolePrefix       = "csgo_econ_action_preview ";
    private const string SteamActionFragment = "+csgo_econ_action_preview ";

    public static bool TryParse(string input, out ParsedInspectData data, out string? error)
    {
        data  = default;
        error = null;

        var decoded        = Uri.UnescapeDataString(input.Trim());
        bool nearChatLimit = decoded.Length >= 118;
        const string chatMsg = "Inspect link hit the chat character limit. " +
                               "Paste the full link into console, or get a short code at cs2inspects.com.";

        if (!TryExtractHex(decoded, out var hex, out error))
        {
            if (nearChatLimit && error != null) error = chatMsg;
            return false;
        }

        if (error != null)
            return false;

        byte[] rawBytes;
        try
        {
            rawBytes = Convert.FromHexString(hex);
        }
        catch (FormatException)
        {
            error = nearChatLimit ? chatMsg : "Invalid inspect link: malformed hex string.";
            return false;
        }

        try
        {
            var proto = UnframeBytes(rawBytes);
            data = DecodeProto(proto);
            return true;
        }
        catch
        {
            error = nearChatLimit ? chatMsg : "Invalid inspect link: could not decode item data.";
            return false;
        }
    }

    private static bool TryExtractHex(string input, out string hex, out string? error)
    {
        hex   = "";
        error = null;

        if (input.StartsWith(ConsolePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = input[ConsolePrefix.Length..].Trim();
            if (IsUnmaskedFormat(candidate))
            {
                error = "Unmasked inspect links (S/A/D format) are not supported. Use a gencode from cs2inspects.com.";
                return false;
            }
            hex = candidate;
            return ValidateHexString(hex, out error);
        }

        if (input.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            var idx = input.IndexOf(SteamActionFragment, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var candidate = input[(idx + SteamActionFragment.Length)..].Trim();
            if (IsUnmaskedFormat(candidate))
            {
                error = "Unmasked inspect links (S/A/D format) are not supported. Use a gencode from cs2inspects.com.";
                return false;
            }
            hex = candidate;
            return ValidateHexString(hex, out error);
        }

        if (IsUnmaskedFormat(input))
        {
            error = "Unmasked inspect links (S/A/D format) are not supported. Use a gencode from cs2inspects.com.";
            return false;
        }

        if (IsRawHex(input))
        {
            hex = input;
            return ValidateHexString(hex, out error);
        }

        return false;
    }

    private static bool IsUnmaskedFormat(string s) =>
        s.Length > 2 && s[0] == 'S' && char.IsDigit(s[1]);

    private static bool IsRawHex(string s) =>
        s.Length >= 16 &&
        s.Any(c => c is (>= 'A' and <= 'F') or (>= 'a' and <= 'f')) &&
        s.All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f'));

    private static bool ValidateHexString(string hex, out string? error)
    {
        error = null;
        if (hex.Length % 2 != 0)
        {
            error = "Invalid inspect link: hex string has an odd number of characters.";
            return false;
        }
        if (hex.Length < 12)
        {
            error = "Invalid inspect link: data too short.";
            return false;
        }
        return true;
    }

    private static ReadOnlySpan<byte> UnframeBytes(byte[] raw)
    {
        if (raw.Length < 6)
            throw new InvalidOperationException("Inspect data too short.");

        var key = raw[0];
        if (key != 0x00)
        {
            for (int i = 0; i < raw.Length; i++)
                raw[i] ^= key;
        }

        return raw.AsSpan(1, raw.Length - 5);
    }

    private static ParsedInspectData DecodeProto(ReadOnlySpan<byte> bytes)
    {
        int    pos           = 0;
        uint   defIndex      = 0, paintIndex = 0, paintSeed = 0, paintwear = 0;
        uint   quality       = 0, killEaterValue = 0;
        string customName    = "";
        var    stickers      = new List<DecodedSubItem>();
        var    keychains     = new List<DecodedSubItem>();

        while (pos < bytes.Length)
        {
            var tag      = (uint)ReadVarint(bytes, ref pos);
            var fieldNum = tag >> 3;
            var wireType = tag & 7;

            switch (fieldNum)
            {
                case 3:  defIndex       = (uint)ReadVarint(bytes, ref pos); break;
                case 4:  paintIndex     = (uint)ReadVarint(bytes, ref pos); break;
                case 6:  quality        = (uint)ReadVarint(bytes, ref pos); break;
                case 7:  paintwear      = (uint)ReadVarint(bytes, ref pos); break;
                case 8:  paintSeed      = (uint)ReadVarint(bytes, ref pos); break;
                case 10: killEaterValue = (uint)ReadVarint(bytes, ref pos); break;
                case 11:
                {
                    var sub = ReadLengthDelimited(bytes, ref pos);
                    customName = Encoding.UTF8.GetString(sub);
                    break;
                }
                case 12:
                {
                    var sub = ReadLengthDelimited(bytes, ref pos);
                    stickers.Add(DecodeSubItem(sub));
                    break;
                }
                case 20:
                {
                    var sub = ReadLengthDelimited(bytes, ref pos);
                    keychains.Add(DecodeSubItem(sub));
                    break;
                }
                default:
                    SkipField(bytes, ref pos, wireType);
                    break;
            }
        }

        return new ParsedInspectData(
            defIndex, paintIndex, paintSeed,
            BitConverter.Int32BitsToSingle((int)paintwear),
            quality, killEaterValue, customName,
            stickers.ToArray(), keychains.ToArray());
    }

    private static DecodedSubItem DecodeSubItem(ReadOnlySpan<byte> bytes)
    {
        int   pos      = 0;
        uint  slot     = 0, id = 0, pattern = 0;
        float wear     = 0, offsetX = 0, offsetY = 0, offsetZ = 0, rotation = 0;

        while (pos < bytes.Length)
        {
            var tag      = (uint)ReadVarint(bytes, ref pos);
            var fieldNum = tag >> 3;
            var wireType = tag & 7;

            switch (fieldNum)
            {
                case 1:  slot     = (uint)ReadVarint(bytes, ref pos); break;
                case 2:  id       = (uint)ReadVarint(bytes, ref pos); break;
                case 3:  wear     = ReadFloat32(bytes, ref pos); break;
                case 4:  ReadFloat32(bytes, ref pos); break; // unused
                case 5:  rotation = ReadFloat32(bytes, ref pos); break;
                case 6:  ReadVarint(bytes, ref pos); break; // unused
                case 7:  offsetX  = ReadFloat32(bytes, ref pos); break;
                case 8:  offsetY  = ReadFloat32(bytes, ref pos); break;
                case 9:  offsetZ  = ReadFloat32(bytes, ref pos); break;
                case 10: pattern  = (uint)ReadVarint(bytes, ref pos); break;
                default: SkipField(bytes, ref pos, wireType); break;
            }
        }

        return new DecodedSubItem(slot, id, wear, offsetX, offsetY, offsetZ, rotation, pattern);
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int pos)
    {
        ulong result = 0;
        int   shift  = 0;

        while (pos < data.Length)
        {
            var b = data[pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift >= 70) throw new InvalidOperationException("Varint too long.");
        }

        throw new InvalidOperationException("Unexpected end of data reading varint.");
    }

    private static float ReadFloat32(ReadOnlySpan<byte> data, ref int pos)
    {
        if (pos + 4 > data.Length)
            throw new InvalidOperationException("Unexpected end of data reading float.");
        var value = BitConverter.ToSingle(data.Slice(pos, 4));
        pos += 4;
        return value;
    }

    private static ReadOnlySpan<byte> ReadLengthDelimited(ReadOnlySpan<byte> data, ref int pos)
    {
        var length = (int)ReadVarint(data, ref pos);
        if (pos + length > data.Length)
            throw new InvalidOperationException("Length-delimited field exceeds data bounds.");
        var slice = data.Slice(pos, length);
        pos += length;
        return slice;
    }

    private static void SkipField(ReadOnlySpan<byte> data, ref int pos, uint wireType)
    {
        switch (wireType)
        {
            case 0: ReadVarint(data, ref pos); break;
            case 1: pos += 8; break;
            case 2:
            {
                var len = (int)ReadVarint(data, ref pos);
                pos += len;
                break;
            }
            case 5: pos += 4; break;
            default: throw new InvalidOperationException($"Unknown protobuf wire type {wireType}.");
        }
    }
}
