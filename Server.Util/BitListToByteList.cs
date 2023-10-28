using System.Collections.Generic;

namespace Server.Util
{
    public static class BitListToByteList
    {
        public static List<byte> Convert(List<bool> bits)
        {
            var i = 0;
            byte result = 0;
            var bytes = new List<byte>();
            for (i = 0; i < bits.Count; i++)
            {
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
                if (i % 8 != 0) result = (byte)(result << (8 - i % 8));
                bytes.Add(result);
            }

            return bytes;
        }
    }
}