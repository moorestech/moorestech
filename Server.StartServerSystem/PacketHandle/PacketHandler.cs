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

        async void SendPackets(Socket client, List<byte[]> packets)
        {
            //一度にまとめて送るとクライアント側でさばき切れないので、0.1秒おきにパケットを送信する
            foreach (var packet in packets)
            {
                client.Send(packet);
                await Task.Delay(10);
            }
        }
    }
}