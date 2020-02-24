using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace BlurSharp.Drawing
{
    public static class BlurHashDrawing
    {
        public static string Encode(Bitmap bufferedImage, int componentX, int componentY)
        {
            BitmapData curBitmapData = bufferedImage.LockBits(
                new Rectangle(0, 0, bufferedImage.Width, bufferedImage.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            int height = curBitmapData.Height;
            int width = curBitmapData.Width;

            // The stride is the width of a single row of pixels (a scan line), rounded up to a four-byte boundary.
            // If the stride is positive, the bitmap is top-down. If the stride is negative, the bitmap is bottom-up.
            //
            int stride = curBitmapData.Stride;
            int dataLength = Math.Abs(stride) * height;

            // We need an unsafe context to get a Span reference to the bitmap data without a copy.
            // We must make sure to get the length correct, and that UnlockBits is not called
            // before we are finished with the bitmapData Span.
            //
            ReadOnlySpan<byte> bitmapData;
            unsafe
            {
                bitmapData = new ReadOnlySpan<byte>(curBitmapData.Scan0.ToPointer(), dataLength);
            }

            // BlurHash encode using the span bytes of the bitmap directly.
            // On Little Endian systems, the subpixel ordering is BGR, where B is the least significant byte.
            // On Big Endian systems, the subpixel ordering is RGB, where R is the least significant byte.
            string result = BlurHash.Encode(bitmapData, BitConverter.IsLittleEndian, stride, width, height, componentX, componentY);
            bufferedImage.UnlockBits(curBitmapData);

            return result;
        }
    }
}
