using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Util
{
    public class ByteListEnumerator
    {
        private readonly List<byte> _payload;
        private int index;

        public ByteListEnumerator(List<byte> payload)
        {
            _payload = payload;
        }

        public byte MoveNextToGetByte()
        {
            return _payload[index++];
        }

        public int MoveNextToGetInt()
        {
            var b = new List<byte>();
            for (var i = 0; i < 4; i++) b.Add(_payload[index++]);

            if (BitConverter.IsLittleEndian) b.Reverse();

            var data = BitConverter.ToInt32(b.ToArray(), 0);
            b.Clear();
            return data;
        }

        public short MoveNextToGetShort()
        {
            var b = new List<byte>();
            for (var i = 0; i < 2; i++) b.Add(_payload[index++]);

            if (BitConverter.IsLittleEndian) b.Reverse();

            var data = BitConverter.ToInt16(b.ToArray(), 0);
            b.Clear();
            return data;
        }

        public float MoveNextToGetFloat()
        {
            var b = new List<byte>();
            for (var i = 0; i < 4; i++) b.Add(_payload[index++]);

            if (BitConverter.IsLittleEndian) b.Reverse();

            var data = BitConverter.ToSingle(b.ToArray(), 0);
            b.Clear();
            return data;
        }


        ///     

        /// <param name="byteNum"> or0</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public string MoveNextToGetString(int byteNum)
        {
            if (byteNum < 0) throw new ArgumentOutOfRangeException($":{byteNum} 0");

            var b = new List<byte>();
            if (byteNum == 0)
                while (index < b.Count)
                    b.Add(_payload[index++]);
            else
                for (var i = 0; i < byteNum; i++)
                    b.Add(_payload[index++]);

            return Encoding.UTF8.GetString(b.ToArray());
        }
    }
}