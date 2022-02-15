using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
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
            if (_socket.Connected)
            {
                _socket.Send(data);
                //送信時にキューに入っているデータを送信する
                while (_sendQueue.Count > 0)
                {
                    Thread.Sleep(10);
                    _socket.Send(_sendQueue.Dequeue());
                }
            }
            else
            {
                //接続していない段階での送信リクエストはキューに入れておく
                _sendQueue.Enqueue(data);
            }
        }
    }
}