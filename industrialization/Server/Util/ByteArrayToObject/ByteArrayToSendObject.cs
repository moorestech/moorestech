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
            return Guid.Empty;
        }
    }
}