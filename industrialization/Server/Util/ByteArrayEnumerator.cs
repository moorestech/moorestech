using System;
using System.Collections.Generic;
using System.Linq;

namespace industrialization.Server.Util
{
    public class ByteArrayEnumerator
    {
        private readonly IEnumerator<byte> _payload;
        public ByteArrayEnumerator(byte[] payload)
        {
            _payload = payload.ToList().GetEnumerator();
        }

        public int MoveNextToGetInt()
        {
            var b = new List<byte>();
            for (int i = 0; i < 4; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToInt32(b.ToArray());
        }

        public short MoveNextToGetShort()
        {
            var b = new List<byte>();
            for (int i = 0; i < 2; i++)
            {
                if (_payload.MoveNext())
                {
                    b.Add(_payload.Current);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("パケットフォーマットの解析に不具合があります");
                }
            }
            return BitConverter.ToInt16(b.ToArray());
        }
    }
}