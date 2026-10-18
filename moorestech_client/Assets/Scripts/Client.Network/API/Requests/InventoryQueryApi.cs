using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Network.API;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.State;
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
            var (response, reason) = await _packetExchangeManager.GetPacketResponseWithReason<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(request, ct);
            ct.ThrowIfCancellationRequested();
            // 通信失敗を画面まで運び、古い在庫を正常表示しない
            // Carry transport failures to the screen instead of keeping stale inventory
            if (reason != PacketWaitCompletionReason.Received)
            {
                var result = reason == PacketWaitCompletionReason.Timeout ? InventoryRequestResult.Timeout : InventoryRequestResult.DeserializeFailed;
                UnityEngine.Debug.LogWarning($"Inventory request failed: {result}");
                return new InventoryResponse(identifier, new List<IItemStack>(), new List<FluidMessagePack>(), result);
            }
            return new InventoryResponse(response.Identifier, CreateStacks(response.Items), response.Fluids.ToList(), response.Result);

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
