using System;
using System.Net.Sockets;
using MainGame.Network.Interface;

namespace MainGame.Network.Send
{
    /// <summary>
    /// ソケットをDIコンテナで直接作ることはできないのでサーバー接続クラスで作ってセットしてもらう
    /// TODO ソケット作成クラスを作るのもアリかもしれない
    /// </summary>
    public class SocketObject : ISocket
    {
        private Socket _socket = null;

        internal void SetSocket(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            if (_socket != null) 
                throw new InvalidOperationException("Socket is already set.");
            
            _socket = socket;
        }

        public void Send(byte[] data)
        {
            _socket.Send(data);
        }
    }
}