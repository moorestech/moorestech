using System.Collections.Generic;

namespace MainGame.Network.Util
{
    public static class ByteListToBitList
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
            return Convert(ByteListConverter.ToByteArray(@short));
        }

        public static List<bool> Convert(int @int)
        {
            return Convert(ByteListConverter.ToByteArray(@int));
        }

        public static List<bool> Convert(byte @byte)
        {
            return Convert(new List<byte> {@byte});
        }

        public static List<bool> Convert(float @float)
        {
            return Convert(ByteListConverter.ToByteArray(@float));
        }
    }
}