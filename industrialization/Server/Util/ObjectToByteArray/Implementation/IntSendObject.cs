using System;

namespace industrialization.Server.Util.ObjectToByteArray.Implementation
{
    public class IntSendObject : ISendObject
    {
        private readonly int _sendData;
        public IntSendObject(int sendData)
        {
            _sendData = sendData;
        }
        public byte[] GetByteArray()
        {
            return BitConverter.GetBytes(_sendData);
        }
    }
}