using System;
using System.Diagnostics;
using System.Net.Sockets;
using MessagePack;
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
                    var error = ReceiveProcess(parser,buffer);
                    if (error)
                    {
                        Console.WriteLine("切断されました");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _client.Close();
                Console.WriteLine("エラーによる切断");
                Console.WriteLine(e);
            }
        }

        private bool ReceiveProcess(PacketBufferParser parser,byte[] buffer)
        {
            int length = _client.Receive(buffer);
            if (length == 0)
            {
                return true;
            }

            //受信データをパケットに分割
            var packets = parser.Parse(buffer, length);

            foreach (var packet in packets)
            {
                var results = _packetResponseCreator.GetPacketResponse(packet);
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Console.WriteLine("add header " + MessagePackSerializer.ConvertToJson(result.ToArray()));
                    result.InsertRange(0, ToByteList.Convert(result.Count));
                    _client.Send(result.ToArray());
                }
            }

            return false;
        }
    }
}