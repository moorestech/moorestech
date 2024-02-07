using System.Collections.Generic;
using Client.Network.NewApi;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using SinglePlay;
using UnityEngine;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly ServerRequester _serverRequester;

        public AllReceivePacketAnalysisService(ServerRequester serverRequester)
        {
            _serverRequester = serverRequester;
        }

        public void Analysis(List<byte> packet)
        {
            var response = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet.ToArray());
            var sequence = response.SequenceId;
            
            
            _serverRequester.ReceiveData(packet,sequence);
        }
    }
}