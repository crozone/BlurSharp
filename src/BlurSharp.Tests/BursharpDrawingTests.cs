using System;
using System.Drawing;
using BlurSharp.Drawing;
using Xunit;

namespace BlurSharp.Tests
{
    public class BursharpDrawingTests
    {
        [Fact]
        public void Test1()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/wave.bmp"));

            string hashResult = BlurHashDrawing.Encode(bitmap, 3, 3);

            Assert.Equal("KDFP~q_NtS00%1NHELIBMc", hashResult);
        }
    }
}
