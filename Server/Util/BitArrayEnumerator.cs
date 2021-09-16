using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Util
{
    public class BitArrayEnumerator
    {
        private List<bool> _bitArray;
        private int index = 0;

        public BitArrayEnumerator(byte[] bytes)
        {
            _bitArray = ByteArrayToBitArray.Convert(bytes).ToList();
        }

        public bool MoveNextToBit()
        {
            var r = _bitArray[index];
            index++;
            return r;
        }
        public byte MoveNextToByte()
        {
            var bitNum = 8;
            
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(_bitArray[i + index]);
            }

            index += bitNum;
            var byteArray = BitArrayToByteArray.Convert(tmpBitArray);
            return byteArray[0];
        }
        public short MoveNextToShort()
        {
            var bitNum = 16;
            
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(_bitArray[i + index]);
            }

            index += bitNum;
            var byteArray = BitArrayToByteArray.Convert(tmpBitArray);
            return BitConverter.ToInt16(byteArray,0);
        }
        public float MoveNextToFloat()
        {
            var bitNum = 32;
            
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(_bitArray[i + index]);
            }

            index += bitNum;
            var byteArray = BitArrayToByteArray.Convert(tmpBitArray);
            return BitConverter.ToSingle(byteArray,0);
        }

        
        public int MoveNextToInt()
        {
            var bitNum = 32;
            
            var tmpBitArray = new List<bool>();
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray.Add(_bitArray[i + index]);
            }

            index += bitNum;
            var byteArray = BitArrayToByteArray.Convert(tmpBitArray);
            return BitConverter.ToInt32(byteArray,0);
        }
    }
}