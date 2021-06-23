using System;

namespace industrialization.Server.ObjectToByteArray.Implementation
{
    public class GuidSendObject : ISendObject
    {
        private Guid _guid;

        public GuidSendObject(Guid guid)
        {
            _guid = guid;
        }
        public byte[] GetByteArray()
        {
            return _guid.ToByteArray();
        }
    }
}