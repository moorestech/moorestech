using System;
using System.Linq;
using MainGame.Network;
using MessagePack;
using Server.Protocol;

namespace Client.Network.API
{
    /// <summary>
    /// パケットの送信だけを行うクラス
    /// 受信が必要な場合は<see cref="PacketExchangeManager"/>を使用してください
    /// </summary>
    public class PacketSender
    {
        private readonly ServerCommunicator _serverCommunicator;
        
        public PacketSender(ServerCommunicator serverCommunicator)
        {
            _serverCommunicator = serverCommunicator;
        }
        
        public void Send(ProtocolMessagePackBase sendData)
        {
            _serverCommunicator.Send(MessagePackSerializer.Serialize(Convert.ChangeType(sendData,sendData.GetType())));
        } 
    }
}