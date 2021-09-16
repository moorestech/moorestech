using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public static class BitArrayToByteArray
    {
        public static byte[] Convert(List<bool> bits)
        {
            int i = 0;
            byte result = 0;
            var bytes = new List<byte>();
            for (i = 0; i < bits.Count; i++)
            {
                if (i == 80)
                {
                    
                }
                if (i != 0 && i % 8 == 0)
                {
                    // 1バイト分で出力しビットカウント初期化
                    bytes.Add(result);
                    result = 0;
                }
                // 指定桁数について1を立てる
                result = (byte)(result << 1);
                if (bits[i]) result |= 1;
            }

            if (i != 0)
            {
                bytes.Add(result);
            }
            return bytes.ToArray();
        }
        public static byte[] Convert(bool[] bits)
        {
            return Convert(bits.ToList());
        }
    }
}