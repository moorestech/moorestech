using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public class BitArrayEnumerator
    {
        private List<byte> bytesList;
        private int index = 0;
        private readonly int[] BIT_MASK = {128 ,64, 32,16,8,4,2,1};

        public BitArrayEnumerator(byte[] bytes)
        {
            bytesList = bytes.ToList();
        }

        public bool MoveNextToBit()
        {
            int r = bytesList[index/8] & BIT_MASK[index%8];
            index++;
            if(r == 0)
            {
                return false;
            }else
            {
                return true;
            }
        }
        public byte MoveNextToByte()
        {
            return GetByteList(8)[0];
        }
        public short MoveNextToShort()
        {
            return BitConverter.ToInt16(GetByteList(16),0);
        }
        public float MoveNextToFloat()
        {
            return BitConverter.ToSingle(GetByteList(32),0);
        }

        
        public int MoveNextToInt()
        {
            return BitConverter.ToInt32(GetByteList(32),0);
        }

        byte[] GetByteList(int bitNum)
        {
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(MoveNextToBit());
            }
            var byteArray = BitArrayToByteArray.Convert(tmpBitArray);
            if(BitConverter.IsLittleEndian)
                Array.Reverse(byteArray);
            return byteArray;
        }
    }
}