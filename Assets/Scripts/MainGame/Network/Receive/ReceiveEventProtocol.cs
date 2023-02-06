using System.Collections.Generic;
using MainGame.Network.Event;
using MainGame.Network.Receive.EventPacket;
using MainGame.Network.Util;
using MessagePack;
using Server.Event.EventReceive;
using UnityEngine;

namespace MainGame.Network.Receive
{
    public class ReceiveEventProtocol : IAnalysisPacket
    {
        readonly Dictionary<string,IAnalysisEventPacket> _eventPacket = new();

        public ReceiveEventProtocol(ReceiveChunkDataEvent receiveChunkDataEvent, ReceiveMainInventoryEvent receiveMainInventoryEvent,
            ReceiveCraftingInventoryEvent receiveCraftingInventoryEvent,ReceiveBlockInventoryEvent receiveBlockInventoryEvent,ReceiveGrabInventoryEvent receiveGrabInventoryEvent)
        {
            _eventPacket.Add(PlaceBlockToSetEventPacket.EventTag,new BlockPlaceEventProtocol(receiveChunkDataEvent));
            _eventPacket.Add(MainInventoryUpdateToSetEventPacket.EventTag,new MainInventorySlotEventProtocol(receiveMainInventoryEvent));
            _eventPacket.Add(OpenableBlockInventoryUpdateToSetEventPacket.EventTag,new BlockInventorySlotUpdateEventProtocol(receiveBlockInventoryEvent));
            _eventPacket.Add(RemoveBlockToSetEventPacket.EventTag,new BlockRemoveEventProtocol(receiveChunkDataEvent));
            _eventPacket.Add(CraftingInventoryUpdateToSetEventPacket.EventTag,new CraftingInventorySlotEventProtocol(receiveCraftingInventoryEvent));
            _eventPacket.Add(GrabInventoryUpdateToSetEventPacket.EventTag,new GrabInventoryUpdateEventProtocol(receiveGrabInventoryEvent));
        }
        
        /// <summary>
        /// イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var tag = MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(data.ToArray()).EventTag;
            
            _eventPacket[tag].Analysis(data);
        }
    }
}