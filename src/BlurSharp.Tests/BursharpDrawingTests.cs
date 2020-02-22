using System;
using System.Drawing;
using BlurSharp.Drawing;
using Xunit;

namespace BlurSharp.Tests
{
    public class BursharpDrawingTests
    {
        private static readonly string Doughnut43Hash = "LlMF%n00%#MwS|WCWEM{R*bbWBbH";
        private static readonly string DoughnutBlackAndWhite43Hash = "LjIY5?00?bIUofWBWBM{WBofWBj[";

        [Fact]
        public void Doughnut43FullTest()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/doughnut.png"));
            string hashResult = BlurHashDrawing.Encode(bitmap, 4, 3);
            Assert.Equal(Doughnut43Hash, hashResult);
        }

        [Fact]
        public void Doughnut43NumComponentsTest()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/doughnut.png"));
            string hashResult = BlurHashDrawing.Encode(bitmap, 4, 3);
            Assert.Equal(Doughnut43Hash.Substring(0, 1), hashResult.Substring(0, 1));
        }

        [Fact]
        public void Doughnut43MaxACTest()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/doughnut.png"));
            string hashResult = BlurHashDrawing.Encode(bitmap, 4, 3);
            Assert.Equal(Doughnut43Hash.Substring(1, 1), hashResult.Substring(1, 1));
        }

        [Fact]
        public void Doughnut43AverageColorTest()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/doughnut.png"));
            string hashResult = BlurHashDrawing.Encode(bitmap, 4, 3);
            Assert.Equal(Doughnut43Hash.Substring(2, 4), hashResult.Substring(2, 4));
        }

        [Fact]
        public void DoughnutBlackAndWhite43FullTest()
        {
            Bitmap bitmap = new Bitmap(Image.FromFile("./img/doughnut_black_and_white.png"));
            string hashResult = BlurHashDrawing.Encode(bitmap, 4, 3);
            Assert.Equal(DoughnutBlackAndWhite43Hash, hashResult);
        }
    }
}
