using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Util;

namespace MainGame.Network.Send
{
    /// <summary>
    /// ソケットをDIコンテナで直接作ることはできないのでサーバー接続クラスで作ってセットしてもらう
    /// </summary>
    public class SocketObject : ISocket
    {
        private readonly Socket _socket;
        public SocketObject(SocketInstanceCreate socketInstanceCreate)
        {
            _socket = socketInstanceCreate.GetSocket();
        }

        public void Send(List<byte> data)
        {
            if (!_socket.Connected) return;
            
            //パケット長を設定
            data.InsertRange(0, ToByteList.Convert((short)data.Count));
            _socket.Send(data.ToArray());
        }

        public void Close()
        {
            _socket.Close();
        }
    }
}