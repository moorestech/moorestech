using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server.PacketHandle
{
    public class PacketHandler
    {
        public void StartServer(PacketResponseCreator packetResponseCreator)
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
            Console.WriteLine("受け入れ準備完了");

            while (true)
            {
                //通信の確率
                var handler = listener.Accept();
                //スレッドでパケットの受信、応答を行う
                new Thread(() =>
                {
                    Socket client = handler;
                    byte[] bytes = new byte[4096];
                    Console.WriteLine("接続確立");
                    while (client.Connected)
                    {
                        client.Receive(bytes);
                        
                        //パケットのレスポンスを得て、送信する
                        packetResponseCreator.GetPacketResponse(bytes.ToList()).ForEach(t => client.Send(t));
                    }
                    Console.WriteLine("切断");
                }).Start();
            }
        }
    }
}