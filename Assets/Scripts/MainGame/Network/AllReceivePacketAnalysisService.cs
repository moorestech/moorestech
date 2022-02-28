using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Event;
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
            INetworkReceivedChunkDataEvent networkReceivedChunkDataEvent,
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            _analysisPacketList.Add(new DummyProtocol());
            _analysisPacketList.Add(new ReceiveChunkDataProtocol(networkReceivedChunkDataEvent));
            _analysisPacketList.Add(new DummyProtocol());//TODO 将来的に他プレイヤー座標のパケットが入る
            _analysisPacketList.Add(new ReceiveEventProtocol(networkReceivedChunkDataEvent,mainInventoryUpdateEvent,craftingInventoryUpdateEvent));
            _analysisPacketList.Add(new ReceivePlayerInventoryProtocol(mainInventoryUpdateEvent,new CraftingInventoryUpdateEvent()));
            _analysisPacketList.Add(new DummyProtocol()); //TODO 地面のマップデータのパケットが入る
            _analysisPacketList.Add(new DummyProtocol()); //TODO ブロックインベントリ応答プロトコルが入る
            
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