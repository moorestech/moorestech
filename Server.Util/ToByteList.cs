using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Util
{
    public static class ToByteList
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
            return Encoding.UTF8.GetBytes(sendData).ToList();
        }
    }
}