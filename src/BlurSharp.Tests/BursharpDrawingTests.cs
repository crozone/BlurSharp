using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using BenchmarkDotNet.Attributes;
using BlurSharp.Drawing;
using Xunit;
using static BlurSharp.Tests.PrecomputedHashes;

namespace BlurSharp.Tests
{
    public class BursharpDrawingTests
    {
        [Fact]
        public void Pic1FullTest()
        {
            TestAllComponentsForImage("pic1.png");
        }

        [Fact]
        public void Pic2FullTest()
        {
            TestAllComponentsForImage("pic2.png");
        }

        [Fact]
        public void Pic3FullTest()
        {
            TestAllComponentsForImage("pic3.png");
        }

        [Fact]
        public void Pic4FullTest()
        {
            TestAllComponentsForImage("pic4.png");
        }

        [Fact]
        public void Pic5FullTest()
        {
            TestAllComponentsForImage("pic5.png");
        }

        [Fact]
        public void Pic6FullTest()
        {
            TestAllComponentsForImage("pic6.png");
        }

        private void TestAllComponentsForImage(string image)
        {
            for (int j = 1; j < 9; j++)
            {
                for (int i = 1; i < 9; i++)
                {
                    string precomputedHash = ImageHashes[(image, j, i)];
                    string hashResult = EncodeImage(image, j, i);
                    Assert.Equal(precomputedHash, hashResult);
                }
            }
        }

        private string EncodeImage(string image, int xComponents, int yComponents)
        {
            string path = Path.Combine(".", "img", image);
            Bitmap bitmap = new Bitmap(Image.FromFile(path));
            return BlurHashDrawing.Encode(bitmap, xComponents, yComponents);
        }
    }
}
