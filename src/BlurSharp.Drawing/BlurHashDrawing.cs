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

            // Stride is like width, but represents the number of bytes in a row,
            // instead of the number of pixels.
            //
            int stride = curBitmapData.Stride;
            int dataLength = stride * height;

            Span<byte> bitmapData;

            // We need an unsafe context to get a Span reference to the bitmap data without a copy.
            // We beed to make sure that we get the length correct, and that UnlockBits is not called
            // before we are finished with the bitmapData Span.
            //
            unsafe
            {
                bitmapData = new Span<byte>(curBitmapData.Scan0.ToPointer(), dataLength);
            }

            string result = BlurHash.Encode(bitmapData, width, height, componentX, componentY);
            bufferedImage.UnlockBits(curBitmapData);

            return result;
        }
    }
}
