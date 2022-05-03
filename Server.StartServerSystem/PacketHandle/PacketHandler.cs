using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Server.Protocol;

namespace Server.StartServerSystem.PacketHandle
{
    public class PacketHandler
    {
        const int Port = 11564;

        public void StartServer(PacketResponseCreator packetResponseCreator)
        {
            //ソケットの作成
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            listener.Bind(new IPEndPoint(IPAddress.Any, Port));
            listener.Listen(10);
            Console.WriteLine("moorestechサーバー 起動準備完了");

            while (true)
            {
                //通信の確率
                var client = listener.Accept();
                Console.WriteLine("接続確立");
                //性能が足りなくなってきたら非同期メソッドを使うようにする
                Task.Run(() => new UserResponse(client, packetResponseCreator).StartListen());
            }
        }
    }
}