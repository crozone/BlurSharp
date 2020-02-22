using System;
using System.Collections.Generic;
using System.Text;

namespace BlurSharp
{
    public static class Base83
    {

        private static readonly char[] chars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G',
            'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
            'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
            'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '#', '$', '%', '*', '+', ',',
            '-', '.', ':', ';', '=', '?', '@', '[', ']', '^', '_', '{', '|', '}', '~' };

        public static string Encode(long value, int length)
        {
            Span<char> buffer = (length * sizeof(byte)) < 1024 ? stackalloc char[length] : new char[length];
            Encode(value, length, buffer);
            return new string(buffer);
        }

        public static void Encode(long value, int length, Span<char> buffer)
        {
            int exp = 1;
            for (int i = 1; i <= length; i++, exp *= 83)
            {
                int digit = (int)(value / exp % 83);
                buffer[length - i] = chars[digit];
            }
        }

        public static int Decode(string value)
        {
            return Decode(value.AsSpan());
        }

        public static int Decode(ReadOnlySpan<char> chars)
        {
            int result = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                result = result * 83 + chars.IndexOf(chars[i]);
            }
            return result;
        }
    }
}
