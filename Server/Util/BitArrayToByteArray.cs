using System.Collections.Generic;
using System.Linq;

namespace industrialization.Server.Util
{
    public class BitArrayToByteArray
    {
        public static byte[] Convert(List<bool> bits)
        {
            int i = 0;
            byte result = 0;
            var bytes = new List<byte>();
            foreach (var bit in bits)
            {
                // 指定桁数について1を立てる
                result = (byte)(result << 1);
                if (bit) result |= 1;
                if (i == 7)
                {
                    // 1バイト分で出力しビットカウント初期化
                    bytes.Add(result);
                    i = 0;
                    result = 0;
                }
                else
                {
                    i++;
                }
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