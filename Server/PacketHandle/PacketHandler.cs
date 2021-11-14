using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
                    //切断されるまでパケットを受信
                    try
                    {
                        while (true)
                        {
                            int length = client.Receive(bytes);
                            if (length == 0)
                            {
                                Console.WriteLine("切断されました");
                                break;
                            }
                            //パケットを受信したら応答を返す
                            var result = packetResponseCreator.GetPacketResponse(bytes.ToList());
                            SendPackets(client, result);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("エラーによる切断");
                        Console.WriteLine(e);
                    }
                }).Start();
            }
        }

        async void SendPackets(Socket client,List<byte[]> packets)
        {
            //一度にまとめて送るとクライアント側でさばき切れないので、0.1秒おきにパケットを送信する
            foreach (var packet in packets)
            {
                client.Send(packet);
                await Task.Delay(100);
            }
        }
    }
}