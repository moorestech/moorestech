using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace industrialization.Server.Util
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
            
            var tmpBitArray = new BitArray(bitNum);
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray[i] = _bitArray[i + index];
            }

            index += bitNum;
            return ToByteArray(tmpBitArray)[0];
        }
        public short MoveNextToShort()
        {
            var bitNum = 16;
            
            var tmpBitArray = new BitArray(bitNum);
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray[i] = _bitArray[i + index];
            }
            
            index += bitNum;
            return BitConverter.ToInt16(ToByteArray(tmpBitArray));
        }
        public float MoveNextToFloat()
        {
            var bitNum = 32;
            
            var tmpBitArray = new BitArray(bitNum);
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray[i] = _bitArray[i + index];
            }
            
            index += bitNum;
            return BitConverter.ToSingle(ToByteArray(tmpBitArray));
        }

        
        public int MoveNextToInt()
        {
            var bitNum = 32;
            
            var tmpBitArray = new BitArray(bitNum);
            for (int i = 0; i < bitNum; i++)
            {
                tmpBitArray[i] = _bitArray[i + index];
            }
            
            index += bitNum;
            return BitConverter.ToInt32(ToByteArray(tmpBitArray));
        }
        byte[] ToByteArray(BitArray bits)
        {
            const int BYTE = 8;
            int length = (bits.Count / BYTE) + ((bits.Count % BYTE == 0) ? 0 : 1);
            var bytes = new byte[length];

            for (int i = 0; i < bits.Length; i++)
            {
                int bitIndex = i % BYTE;
                int byteIndex = i / BYTE;
                
                int mask = (bits[i] ? 1 : 0) << bitIndex;
                bytes[byteIndex] |= (byte) mask;
            }

            return bytes;
        }
    }
}