using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly Dictionary<string,IAnalysisPacket> _analysisPackets = new();
        private int _packetCount = 0;
        
        
        public AllReceivePacketAnalysisService(
            NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent, MainInventoryUpdateEvent mainInventoryUpdateEvent,CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,BlockInventoryUpdateEvent blockInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent,ReceiveInitialHandshakeProtocol receiveInitialHandshakeProtocol)
        {
            _analysisPackets.Add(DummyProtocol.Tag,new ReciveDummyProtocol());
            _analysisPackets.Add(InitialHandshakeProtocol.Tag,receiveInitialHandshakeProtocol);
            _analysisPackets.Add(PlayerCoordinateSendProtocol.ChunkDataTag,new ReceiveChunkDataProtocol(networkReceivedChunkDataEvent)); 
            _analysisPackets.Add(EventProtocolMessagePackBase.EventProtocolTag,new ReceiveEventProtocol(networkReceivedChunkDataEvent,mainInventoryUpdateEvent,craftingInventoryUpdateEvent,blockInventoryUpdateEvent,grabInventoryUpdateEvent));
            _analysisPackets.Add(PlayerInventoryResponseProtocol.Tag,new ReceivePlayerInventoryProtocol(mainInventoryUpdateEvent,craftingInventoryUpdateEvent,grabInventoryUpdateEvent));
            _analysisPackets.Add(BlockInventoryRequestProtocol.Tag,new ReceiveBlockInventoryProtocol(blockInventoryUpdateEvent));

        }

        public void Analysis(List<byte> packet)
        {
            var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet.ToArray()).Tag;

            //receive debug
            _packetCount++;
            if (!_analysisPackets.TryGetValue(tag,out var analyser))
            {
                Debug.LogError("Count " + _packetCount + " NotFoundTag " + tag);
                return;
            }
            Debug.Log("Count " + _packetCount + " Tag " + tag + " " + _analysisPackets[tag].GetType().Name);
            
            
            //analysis packet
            analyser.Analysis(packet);
        }
    }
}