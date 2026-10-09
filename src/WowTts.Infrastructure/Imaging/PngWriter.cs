using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace WowTts.Infrastructure.Imaging;

// Writes an opaque image as a PNG file with .NET's own zlib, so no image library is needed.
// Each row uses PNG's "Sub" filter (store the difference to the pixel on the left), which compresses maps well.
public static class PngWriter
{
    public static void WriteRgb(string path, RgbaImage image)
    {
        int stride = image.Width * 3;
        var raw = new byte[(stride + 1) * image.Height];

        for (int y = 0; y < image.Height; y++)
        {
            int row = y * (stride + 1);
            raw[row] = 1; // filter type: Sub
            for (int x = 0; x < image.Width; x++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    byte value = image.Pixels[(y * image.Width + x) * 4 + channel];
                    byte left = x > 0 ? image.Pixels[(y * image.Width + x - 1) * 4 + channel] : (byte)0;
                    raw[row + 1 + x * 3 + channel] = (byte)(value - left);
                }
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;  // bits per channel
        header[9] = 2;  // colour type: RGB

        using var file = File.Create(path);
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", compressed.ToArray());
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);

        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        stream.Write(typeBytes);
        stream.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typeBytes, data));
        stream.Write(number);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++)
            c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
