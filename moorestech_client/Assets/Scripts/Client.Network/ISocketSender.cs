using System;
using System.Collections.Generic;

namespace MainGame.Network
{
    public interface ISocketSender
    {
        public void Send(List<byte> data);
        public void Close();
        public event Action OnConnected;
    }
}