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

        public ReceiveEventProtocol(ReciveChunkDataEvent reciveChunkDataEvent, MainInventoryUpdateEvent mainInventoryUpdateEvent,
            ReciveCraftingInventoryEvent reciveCraftingInventoryEvent,BlockInventoryUpdateEvent blockInventoryUpdateEvent,GrabInventoryUpdateEvent grabInventoryUpdateEvent)
        {
            _eventPacket.Add(PlaceBlockToSetEventPacket.EventTag,new BlockPlaceEventProtocol(reciveChunkDataEvent));
            _eventPacket.Add(MainInventoryUpdateToSetEventPacket.EventTag,new MainInventorySlotEventProtocol(mainInventoryUpdateEvent));
            _eventPacket.Add(OpenableBlockInventoryUpdateToSetEventPacket.EventTag,new BlockInventorySlotUpdateEventProtocol(blockInventoryUpdateEvent));
            _eventPacket.Add(RemoveBlockToSetEventPacket.EventTag,new BlockRemoveEventProtocol(reciveChunkDataEvent));
            _eventPacket.Add(CraftingInventoryUpdateToSetEventPacket.EventTag,new CraftingInventorySlotEventProtocol(reciveCraftingInventoryEvent));
            _eventPacket.Add(GrabInventoryUpdateToSetEventPacket.EventTag,new GrabInventoryUpdateEventProtocol(grabInventoryUpdateEvent));
        }
        
        /// <summary>
        /// イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var tag = MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(data.ToArray()).EventTag;

            Debug.Log("Event Tag " + tag + " " + _eventPacket[tag].GetType().Name);
            
            _eventPacket[tag].Analysis(data);
        }
    }
}