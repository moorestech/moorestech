using System.Collections.Generic;
using Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Event.EventReceive;
using SinglePlay;
using UnityEngine;

namespace MainGame.Network.Receive.EventPacket
{
    public class BlockInventorySlotUpdateEventProtocol : IAnalysisEventPacket
    {
        private readonly BlockInventoryView _blockInventoryView;
        private readonly ItemStackFactory _itemStackFactory;


        public BlockInventorySlotUpdateEventProtocol(BlockInventoryView blockInventoryView,ItemStackFactory itemStackFactory)
        {
            _blockInventoryView = blockInventoryView;
            _itemStackFactory = itemStackFactory;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer
                .Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(packet.ToArray());
            SetItem(data).Forget();
        }

        private async UniTask SetItem(OpenableBlockInventoryUpdateEventMessagePack data)
        {
            await UniTask.SwitchToMainThread();

            var item = _itemStackFactory.Create(data.Item.Id,data.Item.Count);

            _blockInventoryView.SetItemSlot(item,data.Slot);
        }
    }
}