using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MainGame.Network.Send.SocketUtil;
using UnityEditor.Search;
using UnityEngine;

namespace MainGame.Network.Send
{
    /// <summary>
    /// ソケットをDIコンテナで直接作ることはできないのでサーバー接続クラスで作ってセットしてもらう
    /// </summary>
    public class SocketObject : ISocket
    {
        private readonly Socket _socket = null;
        
        private readonly Queue<byte[]> _sendQueue = new();

        public SocketObject(SocketInstanceCreate socketInstanceCreate)
        {
            _socket = socketInstanceCreate.GetSocket();
        }

        public void Send(byte[] data)
        {
            lock (_sendQueue)
            {
                if (_socket.Connected)
                {
                    _sendQueue.Enqueue(data);
                    var _ = SendPackets();
                }
                else
                {
                    //接続していない段階での送信リクエストはキューに入れておく
                    _sendQueue.Enqueue(data);
                }
            }
        }

        public void Close()
        {
            _socket.Close();
        }

        async Task SendPackets()
        {
            //一度にまとめて送るとサーバー側でさばき切れないので、0.1秒おきにパケットを送信する
            while (true)
            {
                lock (_sendQueue)
                {
                    _socket.Send(_sendQueue.Dequeue());
                    if (_sendQueue.Count == 0)
                    {
                        return;
                    }
                }
                await Task.Delay(10);
            }
        }
    }
}