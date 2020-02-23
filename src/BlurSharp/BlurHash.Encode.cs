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
        /// <param name="stride">The stride is the number of bytes in a row of the image.
        /// This is usually 3 times the width, but may be more due to padding.
        /// In a bottom-up bitmap, the stride will be negative.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <returns>The hash result.</returns>
        public static string Encode(ReadOnlySpan<byte> imageData, int stride, int width, int height, int componentX, int componentY, int maxStackAlloc = 1024)
        {
            // Hashes are 30 characters maximum
            Span<char> hashBuffer = stackalloc char[MaximumHashSize];
            Span<char> hashResult = Encode(imageData, stride, width, height, componentX, componentY, hashBuffer, maxStackAlloc);
            return new string(hashResult);
        }

        /// <summary>
        /// Calculates the blur hash for the given image data.
        /// </summary>
        /// <param name="imageData">The sequence of 24bpp RGB encoded bytes that make up the image.</param>
        /// <param name="stride">The stride is the number of bytes in a row of the image.
        /// This is usually 3 times the width, but may be more due to padding.
        /// In a bottom-up bitmap, the stride will be negative.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <param name="hashBuffer">The character buffer used to output the hash.</param>
        /// <returns>The hash result. This result is a slice of the hashBuffer.</returns>
        public static Span<char> Encode(ReadOnlySpan<byte> imageData, int stride, int width, int height, int componentX, int componentY, Span<char> hashBuffer, int maxStackAlloc = 1024)
        {
            if (componentX < 1 || componentX > 9)
            {
                throw new ArgumentException("Blur hash component X must have a value between 1 and 9");
            }

            if (componentY < 1 || componentY > 9)
            {
                throw new ArgumentException("Blur hash component Y must have a value between 1 and 9");
            }

            if (width > stride)
            {
                throw new ArgumentException($"Width cannot be greater than stride");
            }

            if (Math.Abs(stride) * height != imageData.Length)
            {
                throw new ArgumentException($"Stride times height must be the length of {nameof(imageData)}");
            }

            // Stackalloc if buffer is small enough
            //
            int factorCount = componentX * componentY;
            Span<Factor> factors = (factorCount * 24) < maxStackAlloc ? stackalloc Factor[factorCount] : new Factor[factorCount];

            for (int j = 0; j < componentY; j++)
            {
                for (int i = 0; i < componentX; i++)
                {
                    double normalisation = ((i == 0) && (j == 0)) ? 1 : 2;
                    factors[j * componentX + i] = GetBasis(imageData, stride, width, height, normalisation, i, j);
                }
            }

            Factor dc = factors[0];
            Span<Factor> ac = factors.Slice(1);

            // numberOfComponents + max AC + DC + 2 * AC components
            int hashSize = 1 + 1 + 4 + 2 * (factorCount - 1);
            Span<char> hash = hashBuffer.Slice(0, hashSize);

            // The very first part of the hash is the number of components (1 digit).
            long numberOfComponents = (componentX - 1) + (componentY - 1) * 9;
            Base83.Encode(numberOfComponents, 1, hash);

            // The second part of the hash is the maximum AC component value (1 digit).
            // All AC components are scaled by this value. It represents a floating-point value of (max + 1) / 166.
            double maximumValue;
            if (factors.Length > 0)
            {
                double actualMaximumValue = MaxComponent(ac);
                int quantisedMaximumValue = (int)Math.Max(0, Math.Min(82, Math.Floor(actualMaximumValue * 166 - 0.5)));
                maximumValue = (quantisedMaximumValue + 1) / (double)166;
                Base83.Encode(quantisedMaximumValue, 1, hash.Slice(1));
            }
            else
            {
                maximumValue = 1;
                Base83.Encode(0, 1, hash.Slice(1));
            }

            // The third part of the hash is the average colour of the image (4 digits).
            // The average colour of the image in sRGB space, encoded as a 24-bit RGB value, with R in the most signficant position.            
            Base83.Encode(EncodeDC(dc), 4, hash.Slice(2));

            // The fourth part of the hash is AC components array. (2 digits each, nx * ny - 1 components in total)
            for (int i = 0; i < ac.Length; i++)
            {
                Base83.Encode(EncodeAC(ac[i], maximumValue), 2, hash.Slice(6 + 2 * i));
            }

            return hash;
        }

        private static Factor GetBasis(
            ReadOnlySpan<byte> imageData,
            int stride, int width, int height,
            double normalisation,
            int xComponent, int yComponent)
        {
            double r = 0, g = 0, b = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double basis = Math.Cos((Math.PI * xComponent * x) / width) * Math.Cos((Math.PI * yComponent * y) / height);

                    int rowOffset;
                    if (stride > 0)
                    {
                        // Top-down bitmap
                        rowOffset = y * stride;
                    }
                    else
                    {
                        // Bottom-up bitmap
                        rowOffset = (height - 1 - y) * Math.Abs(stride);
                    }

                    ReadOnlySpan<byte> pixel = imageData.Slice(rowOffset + x * 3, 3);
                    r += basis * SRGBToLinear(pixel[0]);
                    g += basis * SRGBToLinear(pixel[1]);
                    b += basis * SRGBToLinear(pixel[2]);
                }
            }

            double scale = normalisation / (width * height);
            return new Factor()
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

        private static double MaxComponent(Span<Factor> values)
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

        private static double MaxComponent(Factor factor)
        {
            return Math.Max(Math.Abs(factor.R), Math.Max(Math.Abs(factor.G), Math.Abs(factor.B)));
        }

        private static int EncodeDC(Factor value)
        {
            int roundedR = LinearToSRGB(value.R);
            int roundedG = LinearToSRGB(value.G);
            int roundedB = LinearToSRGB(value.B);
            return (roundedR << 16) + (roundedG << 8) + roundedB;
        }

        private static int EncodeAC(Factor value, double maximumValue)
        {
            int quantR = (int)Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.R / maximumValue, 0.5) * 9 + 9.5)));
            int quantG = (int)Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.G / maximumValue, 0.5) * 9 + 9.5)));
            int quantB = (int)Math.Max(0, Math.Min(18, Math.Floor(SignPow(value.B / maximumValue, 0.5) * 9 + 9.5)));
            return quantR * 19 * 19 + quantG * 19 + quantB;
        }

        private static double SignPow(double val, double exp)
        {
            //return Math.CopySign(Math.Pow(Math.Abs(val), exp), val);

            // We don't have access to CopySign in netstandard 2.1, so emulate the behaviour here.

            var value = Math.Pow(Math.Abs(val), exp);
            var sign = Math.Sign(val);
            return Math.Abs(value) * sign;
        }

        private static int LinearToSRGB(double value)
        {
            double v = Math.Max(0, Math.Min(1, value));
            if (v <= 0.0031308)
            {
                return (int)(v * 12.92 * 255 + 0.5);
            }
            else
            {
                return (int)((1.055 * Math.Pow(v, 1 / 2.4) - 0.055) * 255 + 0.5);
            }
        }
    }
}
