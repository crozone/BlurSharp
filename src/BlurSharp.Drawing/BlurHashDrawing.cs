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
            int width = bufferedImage.Width;
            int height = bufferedImage.Height;

            BitmapData curBitmapData = bufferedImage.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            // The stride is the width of a single row of pixels (a scan line), rounded up to a four-byte boundary.
            // If the stride is positive, the bitmap is top-down. If the stride is negative, the bitmap is bottom-up.
            //
            int stride = curBitmapData.Stride;
            int dataLength = Math.Abs(stride) * height;

            ReadOnlySpan<byte> bitmapData;

            // We need an unsafe context to get a Span reference to the bitmap data without a copy.
            // We beed to make sure that we get the length correct, and that UnlockBits is not called
            // before we are finished with the bitmapData Span.
            //
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
