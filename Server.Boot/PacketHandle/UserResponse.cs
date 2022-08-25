using System;
using System.Net.Sockets;
using Server.Protocol;
using Server.Util;

namespace Server.Boot.PacketHandle
{
    public class UserResponse
    {
        private readonly Socket _client;
        private readonly PacketResponseCreator _packetResponseCreator;

        public UserResponse(Socket client,PacketResponseCreator packetResponseCreator)
        {
            _packetResponseCreator = packetResponseCreator;
            _client = client;
        }

        public void StartListen()
        {
            var buffer = new byte[4096];
            //切断されるまでパケットを受信
            try
            {
                var parser = new PacketBufferParser();
                while (true)
                {
                    int length = _client.Receive(buffer);
                    if (length == 0)
                    {
                        Console.WriteLine("切断されました");
                        break;
                    }

                    //受信データをパケットに分割
                    var packets = parser.Parse(buffer, length);
                    
                    foreach (var packet in packets)
                    {
                        var results = _packetResponseCreator.GetPacketResponse(packet);
                        foreach (var result in results)
                        {
                            result.InsertRange(0,ToByteList.Convert((short)result.Count));
                            _client.Send(result.ToArray());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("エラーによる切断");
                Console.WriteLine(e);
            }
        }
    }
}