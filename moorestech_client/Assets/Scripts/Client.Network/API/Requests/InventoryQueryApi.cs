using System.Collections.Generic;
using System.Threading;
using Client.Network.API;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Network.API.Requests
{
    public sealed class InventoryQueryApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly IItemStackFactory _itemStackFactory;

        public InventoryQueryApi(PacketExchangeManager packetExchangeManager, IItemStackFactory itemStackFactory)
        {
            _packetExchangeManager = packetExchangeManager;
            _itemStackFactory = itemStackFactory;
        }

        public async UniTask<PlayerInventoryResponse> GetMyPlayerInventory(CancellationToken ct)
        {
            var request = new PlayerInventoryResponseProtocol.RequestPlayerInventoryProtocolMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack>(request, ct);
            // 装備と選択インデックスも落とさず変換する
            // Preserve equipment and the selected index during conversion
            return new PlayerInventoryResponse(response);
        }

        public async UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await _packetExchangeManager.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Result);

            #region Internal

            // メッセージパックからアイテムスタックを生成
            // Create item stacks from message pack items
            List<IItemStack> CreateStacks(ItemMessagePack[] items)
            {
                var count = items?.Length ?? 0;
                var stacks = new List<IItemStack>(count);
                if (items == null) return stacks;
                foreach (var item in items)
                {
                    stacks.Add(_itemStackFactory.Create(item.Id, item.Count));
                }
                return stacks;
            }

            #endregion
        }
    }
}
