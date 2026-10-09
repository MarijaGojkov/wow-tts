using System.Buffers.Binary;

namespace WowTts.Infrastructure.Imaging;

// An image as RGBA bytes: 4 bytes per pixel, rows from top to bottom.
public sealed record RgbaImage(int Width, int Height, byte[] Pixels);

// Decodes WoW's BLP2 texture format (the largest mipmap only). Covers the encodings the game uses:
// palette (1), DXT1/DXT3/DXT5 (2) and uncompressed BGRA (3). See https://wowdev.wiki/BLP
public static class BlpImage
{
    private const int HeaderSize = 148;   // magic, version, 4 format bytes, size, 16 mip offsets, 16 mip sizes
    private const int PaletteSize = 1024; // 256 BGRA colours, used by palette encoding

    public static RgbaImage Decode(byte[] data)
    {
        if (data.Length < HeaderSize || data[0] != 'B' || data[1] != 'L' || data[2] != 'P' || data[3] != '2')
            throw new InvalidDataException("Not a BLP2 image.");

        byte encoding = data[8];
        byte alphaSize = data[9];
        byte format = data[10];
        int width = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        int height = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(16));
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(20));
        var source = data.AsSpan(offset);
        var pixels = new byte[width * height * 4];

        switch (encoding)
        {
            case 1:
                DecodePalette(data.AsSpan(HeaderSize, PaletteSize), source, alphaSize, width, height, pixels);
                break;
            case 2:
                DecodeDxt(source, format, alphaSize, width, height, pixels);
                break;
            case 3:
                for (int i = 0; i < width * height; i++)
                {
                    pixels[i * 4] = source[i * 4 + 2];
                    pixels[i * 4 + 1] = source[i * 4 + 1];
                    pixels[i * 4 + 2] = source[i * 4];
                    pixels[i * 4 + 3] = source[i * 4 + 3];
                }
                break;
            default:
                throw new InvalidDataException($"Unsupported BLP encoding {encoding}.");
        }

        return new RgbaImage(width, height, pixels);
    }

    private static void DecodePalette(
        ReadOnlySpan<byte> palette, ReadOnlySpan<byte> source, int alphaSize, int width, int height, byte[] pixels)
    {
        int count = width * height;
        var alpha = source[count..];

        for (int i = 0; i < count; i++)
        {
            int color = source[i] * 4;
            pixels[i * 4] = palette[color + 2];
            pixels[i * 4 + 1] = palette[color + 1];
            pixels[i * 4 + 2] = palette[color];
            pixels[i * 4 + 3] = alphaSize switch
            {
                1 => (byte)(((alpha[i / 8] >> (i % 8)) & 1) * 255),
                4 => (byte)(((alpha[i / 2] >> (i % 2 * 4)) & 0xF) * 17),
                8 => alpha[i],
                _ => 255
            };
        }
    }

    // DXT stores 4x4 pixel blocks: 8 bytes of colour, plus 8 bytes of alpha for DXT3 and DXT5.
    private static void DecodeDxt(ReadOnlySpan<byte> source, int format, int alphaSize, int width, int height, byte[] pixels)
    {
        bool dxt3 = format == 1;
        bool dxt5 = format == 7;
        int blockSize = dxt3 || dxt5 ? 16 : 8;
        int blocksWide = Math.Max(1, (width + 3) / 4);
        int blocksHigh = Math.Max(1, (height + 3) / 4);
        Span<byte> colors = stackalloc byte[16];
        Span<byte> alphas = stackalloc byte[16];

        for (int by = 0; by < blocksHigh; by++)
        {
            for (int bx = 0; bx < blocksWide; bx++)
            {
                var block = source.Slice((by * blocksWide + bx) * blockSize, blockSize);
                var colorBlock = blockSize == 16 ? block[8..] : block;

                ushort c0 = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock);
                ushort c1 = BinaryPrimitives.ReadUInt16LittleEndian(colorBlock[2..]);
                uint indices = BinaryPrimitives.ReadUInt32LittleEndian(colorBlock[4..]);
                bool threeColor = format == 0 && c0 <= c1;   // DXT1's mode with a transparent colour
                BuildPalette(c0, c1, threeColor, colors);

                if (dxt3)
                {
                    for (int p = 0; p < 16; p++)
                        alphas[p] = (byte)(((block[p / 2] >> (p % 2 * 4)) & 0xF) * 17);
                }
                else if (dxt5)
                {
                    BuildDxt5Alpha(block, alphas);
                }

                for (int p = 0; p < 16; p++)
                {
                    int x = bx * 4 + p % 4;
                    int y = by * 4 + p / 4;
                    if (x >= width || y >= height)
                        continue;

                    int index = (int)(indices >> (p * 2)) & 3;
                    int target = (y * width + x) * 4;
                    pixels[target] = colors[index * 4];
                    pixels[target + 1] = colors[index * 4 + 1];
                    pixels[target + 2] = colors[index * 4 + 2];
                    pixels[target + 3] = dxt3 || dxt5 ? alphas[p]
                        : threeColor && index == 3 && alphaSize > 0 ? (byte)0
                        : (byte)255;
                }
            }
        }
    }

    // The four colours of a DXT block: two stored as RGB565 and two mixed from them.
    private static void BuildPalette(ushort c0, ushort c1, bool threeColor, Span<byte> colors)
    {
        Rgb565(c0, colors[..4]);
        Rgb565(c1, colors[4..8]);
        for (int channel = 0; channel < 3; channel++)
        {
            int a = colors[channel], b = colors[4 + channel];
            colors[8 + channel] = (byte)(threeColor ? (a + b) / 2 : (2 * a + b) / 3);
            colors[12 + channel] = (byte)(threeColor ? 0 : (a + 2 * b) / 3);
        }
    }

    private static void Rgb565(ushort value, Span<byte> rgb)
    {
        rgb[0] = (byte)((value >> 11 & 0x1F) * 255 / 31);
        rgb[1] = (byte)((value >> 5 & 0x3F) * 255 / 63);
        rgb[2] = (byte)((value & 0x1F) * 255 / 31);
    }

    // DXT5 alpha: two stored values, six or eight in-between steps, and a 3-bit index per pixel.
    private static void BuildDxt5Alpha(ReadOnlySpan<byte> block, Span<byte> alphas)
    {
        int a0 = block[0], a1 = block[1];
        Span<byte> steps = stackalloc byte[8];
        steps[0] = (byte)a0;
        steps[1] = (byte)a1;
        for (int i = 2; i < 8; i++)
        {
            steps[i] = a0 > a1
                ? (byte)(((8 - i) * a0 + (i - 1) * a1) / 7)
                : i < 6 ? (byte)(((6 - i) * a0 + (i - 1) * a1) / 5) : (byte)(i == 6 ? 0 : 255);
        }

        ulong bits = 0;
        for (int i = 0; i < 6; i++)
            bits |= (ulong)block[2 + i] << (8 * i);
        for (int p = 0; p < 16; p++)
            alphas[p] = steps[(int)(bits >> (3 * p)) & 7];
    }
}
