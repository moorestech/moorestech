using System;
using System.Net.Sockets;
using Server.Protocol;
using Server.Util;
using UnityEngine;

namespace Server.Boot.PacketHandle
{
    public class UserResponse
    {
        private readonly Socket _client;
        private readonly PacketResponseCreator _packetResponseCreator;

        public UserResponse(Socket client, PacketResponseCreator packetResponseCreator)
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
                    var error = ReceiveProcess(parser, buffer);
                    if (error)
                    {
                        Debug.Log("切断されました");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _client.Close();
                Debug.Log("エラーによる切断");
                Debug.Log(e);
            }
        }

        private bool ReceiveProcess(PacketBufferParser parser, byte[] buffer)
        {
            var length = _client.Receive(buffer);
            if (length == 0) return true;

            //受信データをパケットに分割
            var packets = parser.Parse(buffer, length);

            foreach (var packet in packets)
            {
                var results = _packetResponseCreator.GetPacketResponse(packet);
                foreach (var result in results)
                {
                    result.InsertRange(0, ToByteList.Convert(result.Count));
                    _client.Send(result.ToArray());
                }
            }

            return false;
        }
    }
}