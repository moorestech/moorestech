using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Core.Item;
using MainGame.UnityView.UI.Inventory.Sub;
using MessagePack;
using Server.Protocol.PacketResponse;
using SinglePlay;

namespace MainGame.Network.Receive
{
    public class ReceiveBlockInventoryProtocol : IAnalysisPacket
    {
        private readonly BlockInventoryView _blockInventoryView;
        private readonly ItemStackFactory _itemStackFactory;


        public ReceiveBlockInventoryProtocol(BlockInventoryView blockInventoryView,SinglePlayInterface singlePlayInterface)
        {
            _blockInventoryView = blockInventoryView;
            _itemStackFactory = singlePlayInterface.ItemStackFactory;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<BlockInventoryResponseProtocolMessagePack>(packet.ToArray());
            SetItem(data).Forget();
        }

        private async UniTask SetItem(BlockInventoryResponseProtocolMessagePack data)
        {
            await UniTask.SwitchToMainThread();
            
            var items = new List<IItemStack>();
            for (int i = 0; i < data.ItemIds.Length; i++)
            {
                items.Add(_itemStackFactory.Create(data.ItemIds[i], data.ItemCounts[i]));
            }

            _blockInventoryView.SetItemList(items);
        }
    }
}