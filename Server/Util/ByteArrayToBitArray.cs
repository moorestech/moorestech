using System.Collections.Generic;
using System.Linq;

namespace industrialization.Server.Util
{
    public class ByteArrayToBitArray
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
        public static bool[] Convert(byte @byte)
        {
            
            return Convert(new List<byte> {@byte});
        }
    }
}