using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public static class ToBitList
    {
        public static List<bool> Convert(List<byte> bytes)
        {
            var r = new List<bool>();
            for (int i = 0; i < bytes.Count; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    int tmp = bytes[i] & 128;
                    tmp >>= 7;
                    r.Add(tmp == 1);
                    bytes[i] = (byte) (bytes[i] << 1);
                }
            }

            return r;
        }

        public static List<bool> Convert(short @short)
        {
            return Convert(ToByteList.Convert(@short));
        }

        public static List<bool> Convert(int @int)
        {
            return Convert(ToByteList.Convert(@int));
        }

        public static List<bool> Convert(byte @byte)
        {
            return Convert(new List<byte> {@byte});
        }

        public static List<bool> Convert(float @float)
        {
            return Convert(ToByteList.Convert(@float));
        }
    }
}