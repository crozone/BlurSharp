using System;
using System.Runtime.InteropServices;

namespace BlurSharp
{
    public static partial class BlurHash
    {
        private const int MaximumHashSize = 1 + 1 + 4 + 2 * (9 * 9 - 1);

        /// <summary>
        /// Calculates the blur hash for the given image data.
        /// </summary>
        /// <param name="imageData">The sequence of 24bpp RGB encoded bytes that make up the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <returns>The hash result.</returns>
        public static string Encode(ReadOnlySpan<byte> imageData, int width, int height, int componentX, int componentY, int maxStackAlloc = 1024)
        {
            // Hashes are 30 characters maximum
            Span<char> hashBuffer = stackalloc char[MaximumHashSize];
            Span<char> hashResult = Encode(imageData, width, height, componentX, componentY, hashBuffer, maxStackAlloc);
            return new string(hashResult);
        }

        /// <summary>
        /// Calculates the blur hash for the given image data.
        /// </summary>
        /// <param name="imageData">The sequence of 24bpp RGB encoded bytes that make up the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <param name="hashBuffer">The character buffer used to output the hash.</param>
        /// <returns>The hash result. This result is a slice of the hashBuffer.</returns>
        public static Span<char> Encode(ReadOnlySpan<byte> imageData, int width, int height, int componentX, int componentY, Span<char> hashBuffer, int maxStackAlloc = 1024)
        {
            if (width * height * 3 != imageData.Length)
            {
                throw new ArgumentException($"Width and height must be 3 times the length of {nameof(imageData)}");
            }

            ReadOnlySpan<Pixel24> pixels = MemoryMarshal.Cast<byte, Pixel24>(imageData);
            return Encode(pixels, width, height, componentX, componentY, hashBuffer, maxStackAlloc);
        }

        /// <summary>
        /// Calculates the blur has for the given pixels.
        /// </summary>
        /// <param name="pixels">The sequence of pixels that make up the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <returns>The hash result.</returns>
        public static string Encode(ReadOnlySpan<Pixel24> pixels, int width, int height, int componentX, int componentY, int maxStackAlloc = 1024)
        {
            // Hashes are 30 characters maximum
            Span<char> hashBuffer = stackalloc char[MaximumHashSize];
            Span<char> hashResult = Encode(pixels, width, height, componentX, componentY, hashBuffer, maxStackAlloc);
            return new string(hashResult);
        }

        /// <summary>
        /// Calculates the blur has for the given pixels.
        /// </summary>
        /// <param name="pixels">The sequence of pixels that make up the image.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <param name="hashBuffer">The character buffer used to output the hash.</param>
        /// <returns>The hash result. This result is a slice of the hashBuffer.</returns>
        public static Span<char> Encode(ReadOnlySpan<Pixel24> pixels, int width, int height, int componentX, int componentY, Span<char> hashBuffer, int maxStackAlloc = 1024)
        {
            if (componentX < 1 || componentX > 9)
            {
                throw new ArgumentException("Blur hash component X must have a value between 1 and 9");
            }

            if (componentY < 1 || componentY > 9)
            {
                throw new ArgumentException("Blur hash component Y must have a value between 1 and 9");
            }

            if (width * height != pixels.Length)
            {
                throw new ArgumentException($"Width and height must be the length of {nameof(pixels)}");
            }

            // Stackalloc if buffer is small enough
            //
            int factorCount = componentX * componentY;
            Span<PixelDoubleFloat> factors = (factorCount * 24) < maxStackAlloc ? stackalloc PixelDoubleFloat[factorCount] : new PixelDoubleFloat[factorCount];

            for (int j = 0; j < componentY; j++)
            {
                for (int i = 0; i < componentX; i++)
                {
                    double normalisation = i == 0 && j == 0 ? 1 : 2;
                    factors[j * componentX + i] = GetBasis(pixels, width, height, normalisation, i, j);
                }
            }

            // numberOfComponents + max AC + DC + 2 * AC components
            int hashSize = 1 + 1 + 4 + 2 * (factorCount - 1);
            Span<char> hash = hashBuffer.Slice(0, hashSize);

            // The very first part of the hash is the number of components (1 digit).
            long numberOfComponents = componentX - 1 + (componentY - 1) * 9;
            Base83.Encode(numberOfComponents, 1, hash);

            // The second part of the hash is the maximum AC component value (1 digit).
            // All AC components are scaled by this value. It represents a floating-point value of (max + 1) / 166.
            double maximumValue;
            if (factors.Length > 1)
            {
                double actualMaximumValue = MaxComponent(factors.Slice(1));
                double quantisedMaximumValue = Math.Floor(Math.Max(0, Math.Min(82, Math.Floor(actualMaximumValue * 166 - 0.5))));
                maximumValue = (quantisedMaximumValue + 1) / 166;
                Base83.Encode((long)Math.Round(quantisedMaximumValue), 1, hash.Slice(1));
            }
            else
            {
                maximumValue = 1;
                Base83.Encode(0, 1, hash.Slice(1));
            }

            // The third part of the hash is the average colour of the image (4 digits).
            // The average colour of the image in sRGB space, encoded as a 24-bit RGB value, with R in the most signficant position.
            PixelDoubleFloat dc = factors[0];
            Base83.Encode(EncodeDC(dc), 4, hash.Slice(2));

            // The fourth part of the hash is AC components array. (2 digits each, nx * ny - 1 components in total)
            for (int i = 0; i < factors.Length; i++)
            {
                Base83.Encode(EncodeAC(factors[i], maximumValue), 2, hash.Slice(6 + 2 * (i - 1)));
            }

            return hash;
        }

        private static PixelDoubleFloat GetBasis(
            ReadOnlySpan<Pixel24> pixels,
            int width, int height,
            double normalisation,
            int i, int j)
        {
            double r = 0, g = 0, b = 0;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double basis = normalisation * Math.Cos((Math.PI * i * x) / width) * Math.Cos((Math.PI * j * y) / height);
                    Pixel24 pixel = pixels[y * width + x];
                    r += basis * SRGBToLinear(pixel.R);
                    g += basis * SRGBToLinear(pixel.G);
                    b += basis * SRGBToLinear(pixel.B);
                }
            }
            double scale = 1.0 / (width * height);
            return new PixelDoubleFloat()
            {
                R = r * scale,
                G = g * scale,
                B = b * scale
            };
        }

        private static double SRGBToLinear(long value)
        {
            double v = value / 255.0;
            if (v <= 0.04045)
            {
                return v / 12.92;
            }
            else
            {
                return Math.Pow((v + 0.055) / 1.055, 2.4);
            }
        }

        private static double MaxComponent(Span<PixelDoubleFloat> values)
        {
            double result = double.NegativeInfinity;
            for (int i = 0; i < values.Length; i++)
            {
                double value = MaxComponent(values[i]);
                if (value > result)
                {
                    result = value;
                }
            }
            return result;
        }

        private static double MaxComponent(PixelDoubleFloat factor)
        {
            return Math.Max(factor.R, Math.Max(factor.G, factor.B));
        }

        private static long EncodeDC(PixelDoubleFloat value)
        {
            long r = LinearTosRGB(value.R);
            long g = LinearTosRGB(value.G);
            long b = LinearTosRGB(value.B);
            return (r << 16) + (g << 8) + b;
        }

        private static long EncodeAC(PixelDoubleFloat value, double maximumValue)
        {
            double quantR = Math.Floor(Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.R / maximumValue, 0.5) * 9 + 9.5))));
            double quantG = Math.Floor(Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.G / maximumValue, 0.5) * 9 + 9.5))));
            double quantB = Math.Floor(Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.B / maximumValue, 0.5) * 9 + 9.5))));
            return (long)Math.Round(quantR * 19 * 19 + quantG * 19 + quantB);
        }

        private static double SignPow(double val, double exp)
        {
            //return Math.CopySign(Math.Pow(Math.Abs(val), exp), val);

            // We don't have access to CopySign in netstandard 2.1, so emulate the behaviour here.

            var value = Math.Pow(Math.Abs(val), exp);
            var sign = Math.Sign(val);
            return Math.Abs(value) * sign;
        }

        private static long LinearTosRGB(double value)
        {
            double v = Math.Max(0, Math.Min(1, value));
            if (v <= 0.0031308)
            {
                return (long)(v * 12.92 * 255 + 0.5);
            }
            else
            {
                return (long)((1.055 * Math.Pow(v, 1 / 2.4) - 0.055) * 255 + 0.5);
            }
        }
    }
}
