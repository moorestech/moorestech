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
            var buffer = new byte[4096];
            //切断されるまでパケットを受信
            try
            {
                var parser = new PacketParser();
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
                        Task.Run(() => ResponsesPacket(packet));
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
                //パケット長を先端に追加
                result.InsertRange(0, ToByteList.Convert((short)result.Count));
                _client.Send(result.ToArray());
            }
        }

        
        
    }
}