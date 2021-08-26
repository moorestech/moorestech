using System;
using System.Collections;

namespace industrialization.Server.Util
{
    public class BitArrayEnumerator
    {
        private BitArray _bitArray;
        private int index = 0;

        public BitArrayEnumerator(byte[] bytes)
        {
            _bitArray = new BitArray(bytes);
        }

        public bool MoveNextToBit()
        {
            return false;
        }
        public byte MoveNextToByte()
        {
            return 0;
        }
        public short MoveNextToShort()
        {
            return 0;
        }
        public float MoveNextToFloat()
        {
            return 0;
        }

        
        public int MoveNextToGetInt()
        {
            var tmpBitArray = new BitArray(32);
            for (int i = 0; i < 32; i++)
            {
                tmpBitArray[i] = _bitArray[i + index];
            }    
            var tmp = new int[1];
            tmpBitArray.CopyTo(tmp, 0);
            return tmp[0];
        }
        
    }
}