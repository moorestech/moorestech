using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGame.Network.Util
{
    public static class ByteListConverter
    {
        public static List<byte> Convert(int sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> Convert(short sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> Convert(float sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result.ToList();
        }

        public static List<byte> Convert(string sendData)
        {
            return System.Text.Encoding.UTF8.GetBytes(sendData).ToList();
        }
    }
}