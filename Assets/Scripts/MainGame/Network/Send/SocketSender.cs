using System;
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
    public class SocketSender : ISocketSender
    {
        private readonly Socket _socket;
        public event Action OnConnected;
        
        public SocketSender(SocketInstanceCreate socketInstanceCreate)
        {
            _socket = socketInstanceCreate.SocketInstance;
            Task.Run(() =>
            {
                while (!_socket.Connected) { }
                OnConnected?.Invoke();
            });
        }

        public void Send(List<byte> data)
        {
            if (!_socket.Connected) return;
            
            //パケット長を設定
            data.InsertRange(0, ToByteList.Convert(data.Count));
            _socket.Send(data.ToArray());
        }

        public void Close()
        {
            _socket.Close();
        }
    }
}