using System.Collections.Generic;
using MainGame.Model.Network.Event;
using MainGame.Network.Receive.EventPacket;
using MainGame.Network.Util;
using UnityEngine;

namespace MainGame.Model.Network.Receive
{
    public class ReceiveEventProtocol : IAnalysisPacket
    {
        readonly List<IAnalysisEventPacket> _eventPacketList = new();

        public ReceiveEventProtocol(NetworkReceivedChunkDataEvent networkReceivedChunkDataEvent, MainInventoryUpdateEvent mainInventoryUpdateEvent,
            CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,BlockInventoryUpdateEvent blockInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _eventPacketList.Add(new BlockPlaceEventProtocol(networkReceivedChunkDataEvent));
            _eventPacketList.Add(new MainInventorySlotEventProtocol(mainInventoryUpdateEvent));
            _eventPacketList.Add(new BlockInventorySlotUpdateEventProtocol(blockInventoryUpdateEvent));
            _eventPacketList.Add(new BlockRemoveEventProtocol(networkReceivedChunkDataEvent));
            _eventPacketList.Add(new CraftingInventorySlotEventProtocol(craftingInventoryUpdateEvent));
            _eventPacketList.Add(new GrabInventoryUpdateEventProtocol(grabInventoryUpdateEvent));
        }
        
        /// <summary>
        /// イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            bytes.MoveNextToGetShort();
            var eventId = bytes.MoveNextToGetShort();
            _eventPacketList[eventId].Analysis(data);
            
            Debug.Log("Event ID " + eventId + " " + _eventPacketList[eventId].GetType().Name);
        }
    }
}