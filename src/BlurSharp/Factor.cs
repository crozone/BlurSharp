using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BlurSharp
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Factor
    {
        public double R;
        public double G;
        public double B;
    }
}
