using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public class BitListEnumerator
    {
        private List<byte> bytesList;
        private int index = 0;
        private readonly int[] BIT_MASK = {128 ,64, 32,16,8,4,2,1};

        public BitListEnumerator(List<byte> bytes)
        {
            bytesList = bytes;
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
            return GetByteArray(8)[0];
        }
        public short MoveNextToShort()
        {
            return BitConverter.ToInt16(GetByteArray(16),0);
        }
        public float MoveNextToFloat()
        {
            return BitConverter.ToSingle(GetByteArray(32),0);
        }

        
        public int MoveNextToInt()
        {
            return BitConverter.ToInt32(GetByteArray(32),0);
        }

        byte[] GetByteArray(int bitNum)
        {
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(MoveNextToBit());
            }
            var byteArray = BitListToByteList.Convert(tmpBitArray).ToArray();
            if(BitConverter.IsLittleEndian)
                Array.Reverse(byteArray);
            return byteArray;
        }
    }
}