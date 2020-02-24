using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace BlurSharp.Drawing
{
    public static class BlurHashDrawing
    {
        // TODO: Create another version of this using an open source, portable image library.
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

            string result = BlurHash.Encode(bitmapData, stride, width, height, componentX, componentY);
            bufferedImage.UnlockBits(curBitmapData);

            return result;
        }
    }
}
