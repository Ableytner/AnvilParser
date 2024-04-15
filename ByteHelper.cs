using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilParser
{
    internal class ByteHelper
    {
        // all functions are big-endian

        public static uint ToUInt8(byte[] data, int offset)
        {
            return (uint)data[offset];
        }

        public static uint ToUInt24(byte[] data, int offset)
        {
            return (uint)((data[offset] << 16) + (data[offset + 1] << 8) + data[offset + 2]);
        }

        public static uint ToUInt32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) + (data[offset + 1] << 16) + (data[offset + 2] << 8) + data[offset + 3]);
        }

        // Code from: https://stackoverflow.com/a/39087852/15436169
        public static int BitCount(int data)
        {
            return (int)(Math.Log(data, 2)) + 1;
        }
    }
}
