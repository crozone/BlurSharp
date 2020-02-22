using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BlurSharp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Pixel24
    {
        public byte R;
        public byte G;
        public byte B;
    }
}
