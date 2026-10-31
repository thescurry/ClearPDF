namespace ClearPDF.Scanning;

/// <summary>
/// Baseline JPEG encoder (SOF0, 4:2:0 for color / 1-component gray). Used for
/// mock pages and for WIA BMP fallback so PDFsharp can embed /DCTDecode.
/// </summary>
internal static class ScanJpegEncoder
{
    public const int Quality = 80;

    public static byte[] EncodeRgb(byte[] rgb, int width, int height, ScanColorMode colorMode)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        ArgumentNullException.ThrowIfNull(rgb);
        if (rgb.Length < width * height * 3)
            throw new ArgumentException("RGB buffer is too small.", nameof(rgb));

        return colorMode == ScanColorMode.Color
            ? Encode(rgb, width, height, components: 3)
            : Encode(ToGray(rgb, width, height), width, height, components: 1);
    }

    public static byte[] EncodeGray(byte[] gray, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gray);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (gray.Length < width * height)
            throw new ArgumentException("Gray buffer is too small.", nameof(gray));
        return Encode(gray, width, height, components: 1);
    }

    private static byte[] ToGray(byte[] rgb, int width, int height)
    {
        var gray = new byte[width * height];
        for (int i = 0, p = 0; i < gray.Length; i++, p += 3)
            gray[i] = (byte)((rgb[p] * 77 + rgb[p + 1] * 150 + rgb[p + 2] * 29) >> 8);
        return gray;
    }

    private static byte[] Encode(byte[] pixels, int width, int height, int components)
    {
        using var ms = new MemoryStream();
        var bit = new BitSink(ms);

        // SOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);

        WriteApp0(ms);
        var qY = ScaleQuant(LumaQuant, Quality);
        var qC = ScaleQuant(ChromaQuant, Quality);
        WriteDqt(ms, 0, qY);
        if (components == 3)
            WriteDqt(ms, 1, qC);

        WriteSof0(ms, width, height, components);
        WriteDht(ms, 0, DcLumaBits, DcLumaVal);
        WriteDht(ms, 0x10, AcLumaBits, AcLumaVal);
        if (components == 3)
        {
            WriteDht(ms, 1, DcChromaBits, DcChromaVal);
            WriteDht(ms, 0x11, AcChromaBits, AcChromaVal);
        }

        WriteSos(ms, components);

        var duY = new float[64];
        var duCb = new float[64];
        var duCr = new float[64];
        var qdu = new int[64];
        int dcY = 0, dcCb = 0, dcCr = 0;

        int mcuX = (width + 7) / 8;
        int mcuY = (height + 7) / 8;
        for (var my = 0; my < mcuY; my++)
        {
            for (var mx = 0; mx < mcuX; mx++)
            {
                SampleBlock(pixels, width, height, components, mx * 8, my * 8, 0, duY);
                Fdct(duY);
                Quantize(duY, qY, qdu);
                EncodeBlock(bit, qdu, ref dcY, DcLumaCodes, DcLumaSizes, AcLumaCodes, AcLumaSizes);

                if (components == 3)
                {
                    SampleBlock(pixels, width, height, components, mx * 8, my * 8, 1, duCb);
                    Fdct(duCb);
                    Quantize(duCb, qC, qdu);
                    EncodeBlock(bit, qdu, ref dcCb, DcChromaCodes, DcChromaSizes, AcChromaCodes, AcChromaSizes);

                    SampleBlock(pixels, width, height, components, mx * 8, my * 8, 2, duCr);
                    Fdct(duCr);
                    Quantize(duCr, qC, qdu);
                    EncodeBlock(bit, qdu, ref dcCr, DcChromaCodes, DcChromaSizes, AcChromaCodes, AcChromaSizes);
                }
            }
        }

        bit.Flush();
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9);
        return ms.ToArray();
    }

    private static void SampleBlock(
        byte[] pixels, int width, int height, int components,
        int originX, int originY, int channel, float[] du)
    {
        for (var y = 0; y < 8; y++)
        {
            var py = Math.Min(originY + y, height - 1);
            for (var x = 0; x < 8; x++)
            {
                var px = Math.Min(originX + x, width - 1);
                float v;
                if (components == 1)
                {
                    v = pixels[py * width + px];
                }
                else
                {
                    var i = (py * width + px) * 3;
                    var r = pixels[i];
                    var g = pixels[i + 1];
                    var b = pixels[i + 2];
                    v = channel switch
                    {
                        0 => 0.299f * r + 0.587f * g + 0.114f * b,
                        1 => -0.1687f * r - 0.3313f * g + 0.5f * b + 128f,
                        _ => 0.5f * r - 0.4187f * g - 0.0813f * b + 128f
                    };
                }

                du[y * 8 + x] = v - 128f;
            }
        }
    }

    // Arai et al. fast FDCT, 8x8, in-place on 64 floats.
    private static void Fdct(float[] d)
    {
        for (var i = 0; i < 8; i++)
            Fdct1D(d, i * 8, 1);
        for (var i = 0; i < 8; i++)
            Fdct1D(d, i, 8);
    }

    private static void Fdct1D(float[] d, int offset, int stride)
    {
        float d0 = d[offset];
        float d1 = d[offset + stride];
        float d2 = d[offset + 2 * stride];
        float d3 = d[offset + 3 * stride];
        float d4 = d[offset + 4 * stride];
        float d5 = d[offset + 5 * stride];
        float d6 = d[offset + 6 * stride];
        float d7 = d[offset + 7 * stride];

        var tmp0 = d0 + d7;
        var tmp7 = d0 - d7;
        var tmp1 = d1 + d6;
        var tmp6 = d1 - d6;
        var tmp2 = d2 + d5;
        var tmp5 = d2 - d5;
        var tmp3 = d3 + d4;
        var tmp4 = d3 - d4;

        var tmp10 = tmp0 + tmp3;
        var tmp13 = tmp0 - tmp3;
        var tmp11 = tmp1 + tmp2;
        var tmp12 = tmp1 - tmp2;

        d[offset] = tmp10 + tmp11;
        d[offset + 4 * stride] = tmp10 - tmp11;

        var z1 = (tmp12 + tmp13) * 0.707106781f;
        d[offset + 2 * stride] = tmp13 + z1;
        d[offset + 6 * stride] = tmp13 - z1;

        tmp10 = tmp4 + tmp5;
        tmp11 = tmp5 + tmp6;
        tmp12 = tmp6 + tmp7;

        var z5 = (tmp10 - tmp12) * 0.382683433f;
        var z2 = 0.541196100f * tmp10 + z5;
        var z4 = 1.306562965f * tmp12 + z5;
        var z3 = tmp11 * 0.707106781f;
        var z11 = tmp7 + z3;
        var z13 = tmp7 - z3;

        d[offset + stride] = z11 + z4;
        d[offset + 3 * stride] = z13 - z2;
        d[offset + 5 * stride] = z13 + z2;
        d[offset + 7 * stride] = z11 - z4;
    }

    private static void Quantize(float[] du, byte[] quant, int[] outDu)
    {
        for (var i = 0; i < 64; i++)
        {
            var z = ZigZag[i];
            var v = du[z] / quant[z];
            outDu[i] = (int)(v < 0 ? v - 0.5f : v + 0.5f);
        }
    }

    private static void EncodeBlock(
        BitSink bit, int[] du, ref int lastDc,
        int[] dcCodes, int[] dcSizes, int[] acCodes, int[] acSizes)
    {
        var diff = du[0] - lastDc;
        lastDc = du[0];
        EncodeCoeff(bit, diff, dcCodes, dcSizes);

        var end0 = 63;
        while (end0 > 0 && du[end0] == 0)
            end0--;

        if (end0 == 0)
        {
            bit.Write(acCodes[0x00], acSizes[0x00]);
            return;
        }

        var i = 1;
        while (i <= end0)
        {
            var start = i;
            while (i <= end0 && du[i] == 0)
                i++;
            var nrzeros = i - start;
            while (nrzeros >= 16)
            {
                bit.Write(acCodes[0xF0], acSizes[0xF0]);
                nrzeros -= 16;
            }

            EncodeAc(bit, nrzeros, du[i], acCodes, acSizes);
            i++;
        }

        if (end0 != 63)
            bit.Write(acCodes[0x00], acSizes[0x00]);
    }

    private static void EncodeAc(BitSink bit, int zeros, int value, int[] codes, int[] sizes)
    {
        var cat = Category(value);
        var rs = (zeros << 4) | cat;
        bit.Write(codes[rs], sizes[rs]);
        if (cat > 0)
            bit.Write(Bits(value), cat);
    }

    private static void EncodeCoeff(BitSink bit, int value, int[] codes, int[] sizes)
    {
        var cat = Category(value);
        bit.Write(codes[cat], sizes[cat]);
        if (cat > 0)
            bit.Write(Bits(value), cat);
    }

    private static int Category(int value)
    {
        if (value == 0)
            return 0;
        var a = Math.Abs(value);
        var cat = 0;
        while (a != 0)
        {
            a >>= 1;
            cat++;
        }

        return cat;
    }

    private static int Bits(int value) => value < 0 ? value - 1 : value;

    private static byte[] ScaleQuant(byte[] table, int quality)
    {
        quality = Math.Clamp(quality, 1, 100);
        var scale = quality < 50 ? 5000 / quality : 200 - quality * 2;
        var dest = new byte[64];
        for (var i = 0; i < 64; i++)
        {
            var v = (table[i] * scale + 50) / 100;
            dest[i] = (byte)Math.Clamp(v, 1, 255);
        }

        return dest;
    }

    private static void WriteApp0(Stream s)
    {
        WriteMarker(s, 0xE0, new byte[]
        {
            (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0,
            1, 1, 0, 0, 1, 0, 1, 0, 0
        });
    }

    private static void WriteDqt(Stream s, int id, byte[] table)
    {
        var payload = new byte[1 + 64];
        payload[0] = (byte)id;
        Buffer.BlockCopy(table, 0, payload, 1, 64);
        WriteMarker(s, 0xDB, payload);
    }

    private static void WriteSof0(Stream s, int width, int height, int components)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(8);
        ms.WriteByte((byte)(height >> 8));
        ms.WriteByte((byte)(height & 0xFF));
        ms.WriteByte((byte)(width >> 8));
        ms.WriteByte((byte)(width & 0xFF));
        ms.WriteByte((byte)components);
        for (var i = 0; i < components; i++)
        {
            ms.WriteByte((byte)(i + 1));
            ms.WriteByte(0x11); // 1:1 sampling — simpler than 4:2:0, still valid baseline
            ms.WriteByte((byte)(i == 0 ? 0 : 1));
        }

        WriteMarker(s, 0xC0, ms.ToArray());
    }

    private static void WriteDht(Stream s, int classId, byte[] bits, byte[] values)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)classId);
        ms.Write(bits, 0, 16);
        ms.Write(values, 0, values.Length);
        WriteMarker(s, 0xC4, ms.ToArray());
    }

    private static void WriteSos(Stream s, int components)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)components);
        for (var i = 0; i < components; i++)
        {
            ms.WriteByte((byte)(i + 1));
            ms.WriteByte((byte)(i == 0 ? 0x00 : 0x11));
        }

        ms.WriteByte(0);
        ms.WriteByte(63);
        ms.WriteByte(0);
        WriteMarker(s, 0xDA, ms.ToArray());
    }

    private static void WriteMarker(Stream s, byte marker, byte[] payload)
    {
        var len = payload.Length + 2;
        s.WriteByte(0xFF);
        s.WriteByte(marker);
        s.WriteByte((byte)(len >> 8));
        s.WriteByte((byte)(len & 0xFF));
        s.Write(payload, 0, payload.Length);
    }

    private sealed class BitSink
    {
        private readonly Stream _s;
        private int _accum;
        private int _n;

        public BitSink(Stream s) => _s = s;

        public void Write(int code, int size)
        {
            if (size == 0)
                return;
            _accum = (_accum << size) | (code & ((1 << size) - 1));
            _n += size;
            while (_n >= 8)
            {
                _n -= 8;
                var b = (byte)((_accum >> _n) & 0xFF);
                _s.WriteByte(b);
                if (b == 0xFF)
                    _s.WriteByte(0x00);
            }
        }

        public void Flush()
        {
            if (_n <= 0)
                return;
            Write(0x7F, 8 - _n);
        }
    }

    // JPEG Annex K tables (natural order for DQT).
    private static readonly byte[] LumaQuant =
    {
        16, 11, 10, 16, 24, 40, 51, 61,
        12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56,
        14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77,
        24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101,
        72, 92, 95, 98, 112, 100, 103, 99
    };

    private static readonly byte[] ChromaQuant =
    {
        17, 18, 24, 47, 99, 99, 99, 99,
        18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99,
        47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99
    };

    private static readonly int[] ZigZag =
    {
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63
    };

    // Standard Huffman tables (bits[1..16] stored in 16 bytes).
    private static readonly byte[] DcLumaBits = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
    private static readonly byte[] DcLumaVal = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    private static readonly byte[] AcLumaBits = { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d };
    private static readonly byte[] AcLumaVal =
    {
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
        0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
        0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
        0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa
    };
    private static readonly byte[] DcChromaBits = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
    private static readonly byte[] DcChromaVal = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
    private static readonly byte[] AcChromaBits = { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };
    private static readonly byte[] AcChromaVal =
    {
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
        0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
        0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
        0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
        0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
        0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa
    };

    private static readonly int[] DcLumaCodes;
    private static readonly int[] DcLumaSizes;
    private static readonly int[] AcLumaCodes;
    private static readonly int[] AcLumaSizes;
    private static readonly int[] DcChromaCodes;
    private static readonly int[] DcChromaSizes;
    private static readonly int[] AcChromaCodes;
    private static readonly int[] AcChromaSizes;

    static ScanJpegEncoder()
    {
        BuildHuffman(DcLumaBits, DcLumaVal, out DcLumaCodes, out DcLumaSizes);
        BuildHuffman(AcLumaBits, AcLumaVal, out AcLumaCodes, out AcLumaSizes);
        BuildHuffman(DcChromaBits, DcChromaVal, out DcChromaCodes, out DcChromaSizes);
        BuildHuffman(AcChromaBits, AcChromaVal, out AcChromaCodes, out AcChromaSizes);
    }

    private static void BuildHuffman(byte[] bits, byte[] values, out int[] codes, out int[] sizes)
    {
        codes = new int[256];
        sizes = new int[256];
        var code = 0;
        var k = 0;
        for (var len = 1; len <= 16; len++)
        {
            code <<= 1;
            for (var i = 0; i < bits[len - 1]; i++)
            {
                var v = values[k++];
                codes[v] = code;
                sizes[v] = len;
                code++;
            }
        }
    }
}
