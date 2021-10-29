using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server.PacketHandle
{
    public static class PacketHandler
    {
        public static void StartServer()
        {
            
            //ここからIPアドレスやポートの設定
            //TODO 外と接続するときは変える必要あると思われる
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11564);
            //ここまでIPアドレスやポートの設定

            //ソケットの作成
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //通信の受け入れ準備
            listener.Bind(localEndPoint);
            listener.Listen(10);

            while (true)
            {
                //通信の確率
                var handler = listener.Accept();
                //スレッドでパケットの受信、応答を行う
                new Thread(() =>
                {
                    Socket client = handler;
                    byte[] bytes = new byte[4096];
                    while (true)
                    {
                        client.Receive(bytes);
                        
                        //パケットのレスポンスを得て、送信する
                        new Thread(() =>
                        {
                            PacketResponseCreator.GetPacketResponse(bytes).ForEach(t => client.Send(t));
                        }).Start();
                    }
                }).Start();
            }
        }
    }
}