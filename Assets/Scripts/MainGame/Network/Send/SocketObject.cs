using System;
using System.Net.Sockets;
using MainGame.Network.Interface;
using MainGame.Network.Send.SocketUtil;

namespace MainGame.Network.Send
{
    /// <summary>
    /// ソケットをDIコンテナで直接作ることはできないのでサーバー接続クラスで作ってセットしてもらう
    /// </summary>
    public class SocketObject : ISocket
    {
        private readonly Socket _socket = null;

        public SocketObject(SocketInstanceCreate socketInstanceCreate)
        {
            _socket = socketInstanceCreate.GetSocket();
        }

        public void Send(byte[] data)
        {
            _socket.Send(data);
        }
    }
}