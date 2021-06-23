using System;

namespace industrialization.Server.Util.ObjectToByteArray.Implementation
{
    public class ShortSendObject
    {
        private readonly short _sendData;
        public ShortSendObject(short sendData)
        {
            _sendData = sendData;
        }
        public byte[] GetByteArray()
        {
            return BitConverter.GetBytes(_sendData);
        }
    }
}