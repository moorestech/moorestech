using System;

namespace industrialization.Server.Util
{
    public static class ByteArrayConverter
    {
        public static byte[] ToByteArray(int sendData)
        {
            return  BitConverter.GetBytes(sendData);
        }
        public static byte[] ToByteArray(short sendData)
        {
            return BitConverter.GetBytes(sendData);
        }
        public static byte[] ToByteArray(float sendData)
        {
            return BitConverter.GetBytes(sendData);
        }
        public static byte[] ToByteArray(string sendData)
        {
            return System.Text.Encoding.UTF8.GetBytes(sendData);
        }
    }
}