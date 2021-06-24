using System;
using System.Collections.Generic;
using System.Linq;

namespace industrialization.Server.Util.ByteArrayToObject
{
    public class ByteArrayToSendObject
    {
        private readonly IEnumerator<byte> _payload;
        public ByteArrayToSendObject(byte[] payload)
        {
            _payload = payload.ToList().GetEnumerator();
        }

        public Guid MoveNextToGetGuid()
        {
            var b = new List<byte>();
            for (int i = 0; i < 16; i++)
            {
                _payload.MoveNext();
                b.Add(_payload.Current);
            }
            return new Guid(b.ToArray());
        }

        public int MoveNextToGetInt()
        {
            var b = new List<byte>();
            for (int i = 0; i < 4; i++)
            {
                _payload.MoveNext();
                b.Add(_payload.Current);
            }
            return BitConverter.ToInt32(b.ToArray());
        }

        public short MoveNextToGetShort()
        {
            var b = new List<byte>();
            for (int i = 0; i < 2; i++)
            {
                _payload.MoveNext();
                b.Add(_payload.Current);
            }
            return BitConverter.ToInt16(b.ToArray());
        }
    }
}