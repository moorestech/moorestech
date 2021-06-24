using System;

namespace industrialization.Server.Util
{
    public static class ByteArrayConverter
    {
        public static byte[] ToByteArray(Guid guid)
        {
            return guid.ToByteArray();
        }
        public static byte[] ToByteArray(int sendData)
        {
            return  BitConverter.GetBytes(sendData);
        }
        public static byte[] ToByteArray(short sendData)
        {
            return BitConverter.GetBytes(sendData);
        }
    }
}