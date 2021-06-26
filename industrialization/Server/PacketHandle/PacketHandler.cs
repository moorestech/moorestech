﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using industrialization.Server.PacketResponse;

namespace industrialization.Server.PacketHandle
{
    public class PacketHandler
    {
        public static void StartServer()
        {
            
            //ここからIPアドレスやポートの設定
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
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
                    byte[] bytes = new byte[1024];
                    while (true)
                    {
                        client.Receive(bytes);
                        //パケットのレスポンスを得て、送信する
                        client.Send(PacketResponseFactory.GetPacketResponse(bytes));
                    }
                }).Start();
            }
        }
    }
}