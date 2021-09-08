using System;
using System.Collections.Generic;
using System.Linq;

namespace industrialization.Server.Util
{
    public static class ByteArrayBitArrayConverter
    {
        
        public static byte[] ToByteArray(List<bool> bits)
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
        public static byte[] ToByteArray(bool[] bits)
        {
            return ToByteArray(bits.ToList());
        }
        
        public static bool[] ToBoolList(List<byte> bytes)
        {
            var r = new List<bool>();
            for (int i = 0; i < bytes.Count; i++)
            {
                for(int j = 0; j < 8; j++)
                {
                    r.Add(bytes[i] % 2 != 0); 
                    bytes[i] = (byte)(bytes[i] >> 1);
                }
            }

            return r.ToArray();
        }

        public static bool[] ToBoolList(byte[] bytes)
        {
            return ToBoolList(bytes.ToList());
        }
        
    }
}