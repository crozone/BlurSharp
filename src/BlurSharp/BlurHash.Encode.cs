using System;
using System.Numerics;

namespace BlurSharp
{
    public static partial class BlurHash
    {
        private const int MaximumHashSize = 1 + 1 + 4 + 2 * (9 * 9 - 1);

        /// <summary>
        /// Calculates the blur hash for the given image data.
        /// </summary>
        /// <param name="imageData">The sequence of 24bpp RGB encoded bytes that make up the image.</param>
        /// <param name="bgrOrder">If false, the imageData is treated as RGB, with R in the zero index.
        /// If true, the imageData is treated as BGR, with B in the zero index.</param>
        /// <returns>The hash result.</returns>
        /// <param name="stride">The stride is the number of bytes in a row of the image.
        /// This is usually 3 times the width, but may be more due to padding.
        /// In a bottom-up bitmap, the stride will be negative.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        public static string Encode(ReadOnlySpan<byte> imageData, bool bgrOrder, int stride, int width, int height, int componentX, int componentY, int maxStackAlloc = 1024)
        {
            // Hashes are 30 characters maximum
            Span<char> hashBuffer = stackalloc char[MaximumHashSize];
            Span<char> hashResult = Encode(imageData, bgrOrder, stride, width, height, componentX, componentY, hashBuffer, maxStackAlloc);
            return new string(hashResult);
        }

        /// <summary>
        /// Calculates the blur hash for the given image data.
        /// </summary>
        /// <param name="imageData">The sequence of 24bpp RGB encoded bytes that make up the image.</param>
        /// <param name="bgrOrder">If false, the imageData is treated as RGB, with R in the zero index.
        /// If true, the imageData is treated as BGR, with B in the zero index.</param>
        /// <returns>The hash result.</returns>
        /// <param name="stride">The stride is the number of bytes in a row of the image.
        /// This is usually 3 times the width, but may be more due to padding.
        /// In a bottom-up bitmap, the stride will be negative.</param>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="componentX">The number of componants in the output hash, in the X axis.</param>
        /// <param name="componentY">The number of componants in the output hash, in the Y axis.</param>
        /// <param name="hashBuffer">The character buffer used to output the hash.</param>
        /// <returns>The hash result. This result is a slice of the hashBuffer.</returns>
        public static Span<char> Encode(ReadOnlySpan<byte> imageData, bool bgrOrder, int stride, int width, int height, int componentX, int componentY, Span<char> hashBuffer, int maxStackAlloc = 2048)
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
            Span<Vector3> factors = (factorCount * 24) < maxStackAlloc ? stackalloc Vector3[factorCount] : new Vector3[factorCount];

            for (int j = 0; j < componentY; j++)
            {
                for (int i = 0; i < componentX; i++)
                {
                    float normalisation = ((i == 0) && (j == 0)) ? 1 : 2;
                    factors[j * componentX + i] = GetBasis(i, j, stride, width, height, normalisation, imageData, bgrOrder);
                }
            }

            Vector3 dc = factors[0];
            Span<Vector3> ac = factors.Slice(1);

            // numberOfComponents + max AC + DC + 2 * AC components
            int hashSize = 1 + 1 + 4 + 2 * (factorCount - 1);
            Span<char> hash = hashBuffer.Slice(0, hashSize);

            // The very first part of the hash is the number of components (1 digit).
            long numberOfComponents = (componentX - 1) + (componentY - 1) * 9;
            Base83.Encode(numberOfComponents, 1, hash);

            // The second part of the hash is the maximum AC component value (1 digit).
            // All AC components are scaled by this value. It represents a floating-point value of (max + 1) / 166.
            float maximumValue;
            if (ac.Length > 0)
            {
                float actualMaximumValue = MaxComponent(ac);
                int quantisedMaximumValue = Math.Max(0, Math.Min(82, (int)(actualMaximumValue * 166 - 0.5f)));
                maximumValue = (quantisedMaximumValue + 1) / 166.0f;
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

        private static Vector3 GetBasis(
            int xComponent, int yComponent,
            int stride, int width, int height,
            float normalisation,
            ReadOnlySpan<byte> imageData,
            bool bgrOrder)
        {
            Vector3 factorSum = new Vector3(0, 0, 0);

            int strideAbs = Math.Abs(stride);

            float xMultiplier = MathF.PI * xComponent / width;
            float yMultiplier = MathF.PI * yComponent / height;

            for (int y = 0; y < height; y++)
            {
                int pixelRowOffset;
                if (stride > 0)
                {
                    // Top-down bitmap
                    pixelRowOffset = (y * strideAbs);
                }
                else
                {
                    // Bottom-up bitmap
                    pixelRowOffset = (((height - y) - 1) * strideAbs);
                }

                for (int x = 0; x < width; x++)
                {
                    float basis = MathF.Cos(xMultiplier * x) * MathF.Cos(yMultiplier * y);

                    int pixelOffset = pixelRowOffset + (x * 3);
                    ReadOnlySpan<byte> pixel = imageData.Slice(pixelOffset, 3);

                    Vector3 vectorPixel;

                    if (bgrOrder)
                    {
                        vectorPixel = new Vector3(Precalculated.SrgbToLinearF[pixel[2]], Precalculated.SrgbToLinearF[pixel[1]], Precalculated.SrgbToLinearF[pixel[0]]);
                    }
                    else
                    {
                        vectorPixel = new Vector3(Precalculated.SrgbToLinearF[pixel[0]], Precalculated.SrgbToLinearF[pixel[1]], Precalculated.SrgbToLinearF[pixel[2]]);
                    }

                    factorSum += vectorPixel * basis;
                }
            }

            float scale = normalisation / (width * height);
            return factorSum * scale;
        }

        private static float SRGBToLinear(byte value)
        {
            float v = value / 255.0f;
            if (v <= 0.04045f)
            {
                return v / 12.92f;
            }
            else
            {
                return MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
            }
        }

        private static float MaxComponent(Span<Vector3> values)
        {
            Vector3 maxVector = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < values.Length; i++)
            {
                maxVector = Vector3.Max(Vector3.Abs(values[i]), maxVector);
            }

            return MathF.Max(maxVector.X, MathF.Max(maxVector.Y, maxVector.Z));
        }

        private static int EncodeDC(Vector3 value)
        {
            int roundedR = LinearToSRGB(value.X);
            int roundedG = LinearToSRGB(value.Y);
            int roundedB = LinearToSRGB(value.Z);
            return (roundedR << 16) + (roundedG << 8) + roundedB;
        }

        private readonly static Vector3 vector18 = new Vector3(18, 18, 18);
        private readonly static Vector3 vector95 = new Vector3(9.5f, 9.5f, 9.5f);

        private static int EncodeAC(Vector3 value, float maximumValue)
        {
            Vector3 scaledValue = value / maximumValue;
            Vector3 calc = Vector3.Clamp(CopySign(Vector3.SquareRoot(Vector3.Abs(scaledValue)), scaledValue) * 9 + vector95, Vector3.Zero, vector18);
            return (int)calc.X * 19 * 19 + (int)calc.Y * 19 + (int)calc.Z;
        }

        private static Vector3 CopySign(Vector3 value, Vector3 sign)
        {
            Vector3 signs = new Vector3(MathF.Sign(sign.X), MathF.Sign(sign.Y), MathF.Sign(sign.Z));
            Vector3 valueAbs = Vector3.Abs(value);
            return valueAbs * signs;
        }

        private static int LinearToSRGB(float value)
        {
            float val = MathF.Max(0, MathF.Min(1, value));
            if (val <= 0.0031308f)
            {
                return (int)(val * 12.92f * 255 + 0.5f);
            }
            else
            {
                return (int)((1.055f * MathF.Pow(val, 1 / 2.4f) - 0.055f) * 255 + 0.5f);
            }
        }
    }
}
