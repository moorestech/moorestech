using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public static class ByteArrayToBitArray
    {
        public static bool[] Convert(List<byte> bytes)
        {
            var r = new List<bool>();
            for (int i = 0; i < bytes.Count; i++)
            {
                for(int j = 0; j < 8; j++)
                {
                    int tmp = bytes[i] & 128;
                    tmp >>= 7;
                    r.Add(tmp == 1); 
                    bytes[i] = (byte)(bytes[i] << 1);
                }
            }

            return r.ToArray();
        }

        public static bool[] Convert(byte[] bytes)
        {
            return Convert(bytes.ToList());
        }

        public static bool[] Convert(short @short)
        {
            return Convert(ByteArrayConverter.ToByteArray(@short));
        }
        public static bool[] Convert(int @int)
        {
            return Convert(ByteArrayConverter.ToByteArray(@int));
        }
        public static bool[] Convert(byte @byte)
        {
            
            return Convert(new List<byte> {@byte});
        }
        public static bool[] Convert(float @float)
        {
            return Convert(ByteArrayConverter.ToByteArray(@float));
        }
    }
}