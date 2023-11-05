using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
using Server.Util;

namespace MainGame.Network.Send
{
    /// <summary>
    ///     ソケットをDIコンテナで直接作ることはできないのでサーバー接続クラスで作ってセットしてもらう
    /// </summary>
    public class SocketSender : ISocketSender
    {
        private readonly Socket _socket;

        public SocketSender(SocketInstanceCreate socketInstanceCreate)
        {
            _socket = socketInstanceCreate.SocketInstance;
            Task.Run(async () =>
            {
                while (!_socket.Connected) await Task.Delay(100);
                OnConnected?.Invoke();
            });
        }

        public event Action OnConnected;

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