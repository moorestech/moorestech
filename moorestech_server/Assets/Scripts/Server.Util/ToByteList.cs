using System;
using System.Text;

namespace Server.Util
{
    public static class ToByteArray
    {
        public static byte[] Convert(int sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }

        public static byte[] Convert(short sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }

        public static byte[] Convert(float sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }

        public static byte[] Convert(string sendData)
        {
            return Encoding.UTF8.GetBytes(sendData);
        }
    }
}
