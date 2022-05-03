using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Server.Protocol;
using Server.Util;

namespace Server.StartServerSystem.PacketHandle
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
            byte[] bytes = new byte[10];
            //切断されるまでパケットを受信
            try
            {
                var parser = new PacketParser();
                while (true)
                {
                    int length = _client.Receive(bytes);
                    if (length == 0)
                    {
                        Console.WriteLine("切断されました");
                        break;
                    }

                    var packets = parser.Parse(bytes, length);
                    
                    foreach (var packet in packets)
                    {
                        //Task.Run(() => ResponsesPacket(packet));
                        Console.WriteLine(Encoding.UTF8.GetString(packet.ToArray()));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("エラーによる切断");
                Console.WriteLine(e);
            }
        }


        private async Task ResponsesPacket(List<byte> bytes)
        {
            var results = await Task.Run(() => _packetResponseCreator.GetPacketResponse(bytes));
            foreach (var result in results)
            {
                _client.Send(result);
            }
        }

        
        
    }
}