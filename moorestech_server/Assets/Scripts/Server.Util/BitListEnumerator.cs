using System;
using System.Collections.Generic;

namespace Server.Util
{
    public class BitListEnumerator
    {
        private readonly int[] _bitMask = { 128, 64, 32, 16, 8, 4, 2, 1 };
        private readonly List<byte> _bytesList;
        private int _index;

        public BitListEnumerator(List<byte> bytes)
        {
            _bytesList = bytes;
        }

        public bool MoveNextToBit()
        {
            var r = _bytesList[_index / 8] & _bitMask[_index % 8];
            _index++;
            if (r == 0)
                return false;
            return true;
        }

        public byte MoveNextToByte()
        {
            return GetByteArray(8)[0];
        }

        public short MoveNextToShort()
        {
            return BitConverter.ToInt16(GetByteArray(16), 0);
        }

        public float MoveNextToFloat()
        {
            return BitConverter.ToSingle(GetByteArray(32), 0);
        }


        public int MoveNextToInt()
        {
            return BitConverter.ToInt32(GetByteArray(32), 0);
        }

        private byte[] GetByteArray(int bitNum)
        {
            var tmpBitArray = new List<bool>();
            for (var i = 0; i < bitNum; i++) tmpBitArray.Add(MoveNextToBit());

            var byteArray = BitListToByteList.Convert(tmpBitArray).ToArray();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(byteArray);
            return byteArray;
        }
    }
}