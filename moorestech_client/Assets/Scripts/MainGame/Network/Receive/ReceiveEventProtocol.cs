using System.Collections.Generic;
using Core.Item;
using MainGame.Network.Event;
using MainGame.Network.Receive.EventPacket;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive
{
    public class ReceiveEventProtocol : IAnalysisPacket
    {
        private readonly Dictionary<string, IAnalysisEventPacket> _eventPacket = new();

        //TODO ここはDIコンテナを渡すほうがいいのでは
        public ReceiveEventProtocol(ReceiveChunkDataEvent receiveChunkDataEvent,BlockInventoryView blockInventoryView, ReceiveBlockStateChangeEvent receiveBlockStateChangeEvent, ReceiveUpdateMapObjectEvent receiveUpdateMapObjectEvent,
            LocalPlayerInventoryDataController localPlayerInventoryDataController, ItemStackFactory itemStackFactory,InventoryMainAndSubCombineItems inventoryMainAndSubCombineItems)
        {
            _eventPacket.Add(PlaceBlockToSetEventPacket.EventTag, new BlockPlaceEventProtocol(receiveChunkDataEvent));
            _eventPacket.Add(MainInventoryUpdateToSetEventPacket.EventTag, new MainInventorySlotEventProtocol(itemStackFactory,inventoryMainAndSubCombineItems));
            _eventPacket.Add(OpenableBlockInventoryUpdateToSetEventPacket.EventTag, new BlockInventorySlotUpdateEventProtocol(blockInventoryView,itemStackFactory));
            _eventPacket.Add(RemoveBlockToSetEventPacket.EventTag, new BlockRemoveEventProtocol(receiveChunkDataEvent));
            _eventPacket.Add(GrabInventoryUpdateToSetEventPacket.EventTag, new GrabInventoryUpdateEventProtocol(localPlayerInventoryDataController,itemStackFactory));
            _eventPacket.Add(ChangeBlockStateEventPacket.EventTag, new BlockStateChangeEventProtocol(receiveBlockStateChangeEvent));
            _eventPacket.Add(MapObjectUpdateEventPacket.EventTag, new MapObjectUpdateEventProtocol(receiveUpdateMapObjectEvent));
        }

        /// <summary>
        ///     イベントのパケットを受け取り、さらに個別の解析クラスに渡す
        /// </summary>
        /// <param name="data"></param>
        public void Analysis(List<byte> data)
        {
            var tag = MessagePackSerializer.Deserialize<EventProtocolMessagePackBase>(data.ToArray()).EventTag;

            _eventPacket[tag].Analysis(data);
        }
    }
}