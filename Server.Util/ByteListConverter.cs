using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public static class ByteListConverter
    {
        public static List<byte> ToByteArray(int sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> ToByteArray(short sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> ToByteArray(float sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> ToByteArray(string sendData)
        {
            return System.Text.Encoding.UTF8.GetBytes(sendData).ToList();
        }
    }
}