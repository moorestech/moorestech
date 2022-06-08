using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Event;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly Dictionary<string,IAnalysisPacket> _analysisPacketList = new();
        private int _packetCount = 0;
        
        
        public AllReceivePacketAnalysisService(
            NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent, MainInventoryUpdateEvent mainInventoryUpdateEvent,CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,BlockInventoryUpdateEvent blockInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _analysisPacketList.Add(DummyProtocol.Tag,new ReciveDummyProtocol());
            _analysisPacketList.Add(PlayerCoordinateSendProtocol.ChunkDataTag,new ReceiveChunkDataProtocol(networkReceivedChunkDataEvent)); 
            _analysisPacketList.Add(EventProtocol.Tag,new ReceiveEventProtocol(networkReceivedChunkDataEvent,mainInventoryUpdateEvent,craftingInventoryUpdateEvent,blockInventoryUpdateEvent,grabInventoryUpdateEvent));
            _analysisPacketList.Add(PlayerInventoryResponseProtocol.Tag,new ReceivePlayerInventoryProtocol(mainInventoryUpdateEvent,craftingInventoryUpdateEvent,grabInventoryUpdateEvent));
            _analysisPacketList.Add(BlockInventoryRequestProtocol.Tag,new ReceiveBlockInventoryProtocol(blockInventoryUpdateEvent));
            
        }

        public void Analysis(List<byte> packet)
        {
            var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet.ToArray()).Tag;


            //receive debug
            _packetCount++;
            Debug.Log("Count " + _packetCount + " Tag " + tag + " " + _analysisPacketList[tag].GetType().Name);
            
            
            //analysis packet
            _analysisPacketList[tag].Analysis(packet);
        }
    }
}