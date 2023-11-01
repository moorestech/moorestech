using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Protocol;
using UnityEngine;

namespace Server.Boot.PacketHandle
{
    public class PacketHandler
    {
        private const int Port = 11564;

        public void StartServer(PacketResponseCreator packetResponseCreator)
        {
            //ソケットの作成
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(10);
            Debug.Log("moorestechサーバー 起動完了");

            while (true)
            {
                //通信の確率
                var client = listener.Accept();
                Debug.Log("接続確立");
                //性能が足りなくなってきたら非同期メソッドを使うようにする
                Task.Run(() => new UserResponse(client, packetResponseCreator).StartListen());
            }
        }
    }
}