using System.Collections.Generic;
using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Network.API
{
    internal sealed class InventoryQueryApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;
        private readonly IItemStackFactory _itemStackFactory;

        public InventoryQueryApi(PacketExchangeManager packetExchangeManager, IItemStackFactory itemStackFactory)
        {
            _packetExchangeManager = packetExchangeManager;
            _itemStackFactory = itemStackFactory;
        }

        public async UniTask<InventoryResponse> GetInventory(InventoryIdentifierMessagePack identifier, CancellationToken ct)
        {
            var request = new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier);
            var response = await _packetExchangeManager.GetPacketResponse<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Result);
        }

        private List<IItemStack> CreateStacks(ItemMessagePack[] items)
        {
            // メッセージパックからアイテムスタックを生成
            // Create item stacks from message pack items
            var count = items?.Length ?? 0;
            var stacks = new List<IItemStack>(count);
            if (items == null) return stacks;
            foreach (var item in items)
            {
                stacks.Add(_itemStackFactory.Create(item.Id, item.Count));
            }
            return stacks;
        }
    }

    public class InventoryResponse
    {
        public InventoryIdentifierMessagePack Identifier { get; }
        public List<IItemStack> Items { get; }
        public InventoryRequestResult Result { get; }

        public InventoryResponse(InventoryIdentifierMessagePack identifier, List<IItemStack> items, InventoryRequestResult result)
        {
            Identifier = identifier;
            Items = items;
            Result = result;
        }
    }
}
