using System.Collections.Generic;

namespace MainGame.Network
{
    public interface ISocket
    {
        public void Send(List<byte> data);
        public void Close();
    }
}