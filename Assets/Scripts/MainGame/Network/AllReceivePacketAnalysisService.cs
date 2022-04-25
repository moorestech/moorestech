using System.Collections.Generic;
using System.Linq;
using MainGame.Model.Network.Event;
using MainGame.Model.Network.Receive;
using MainGame.Network.Receive;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Network
{
    public class AllReceivePacketAnalysisService
    {
        private readonly List<IAnalysisPacket> _analysisPacketList = new List<IAnalysisPacket>();
        private int _packetCount = 0;
        
        
        public AllReceivePacketAnalysisService(
            NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent, MainInventoryUpdateEvent mainInventoryUpdateEvent,CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,BlockInventoryUpdateEvent blockInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new ReceiveChunkDataProtocol(networkReceivedChunkDataEvent));
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new ReceiveEventProtocol(networkReceivedChunkDataEvent,mainInventoryUpdateEvent,craftingInventoryUpdateEvent,blockInventoryUpdateEvent,grabInventoryUpdateEvent));
            _analysisPacketList.Add(new ReceivePlayerInventoryProtocol(mainInventoryUpdateEvent,craftingInventoryUpdateEvent));
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new ReceiveBlockInventoryProtocol(blockInventoryUpdateEvent));
            
        }

        public void Analysis(byte[] bytes)
        {
            //get packet id
            var bytesList = bytes.ToList();
            var packetId = new ByteArrayEnumerator(bytesList).MoveNextToGetShort();

            
            //receive debug
            _packetCount++;
            Debug.Log("Count " + _packetCount + " ID " + packetId + " " + _analysisPacketList[packetId].GetType().Name);
            
            
            //analysis packet
            _analysisPacketList[packetId].Analysis(bytesList);
        }
    }
}