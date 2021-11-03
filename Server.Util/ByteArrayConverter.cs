using System;

namespace Server.Util
{
    public static class ByteArrayConverter
    {
        public static byte[] ToByteArray(int sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
        public static byte[] ToByteArray(short sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
        public static byte[] ToByteArray(float sendData)
        {
            var result = BitConverter.GetBytes(sendData);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(result);
            return result;
        }
        public static byte[] ToByteArray(string sendData)
        {
            return System.Text.Encoding.UTF8.GetBytes(sendData);
        }
    }
}