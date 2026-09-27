using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.WebUiHost.Game.Topics.BlockDetail;

namespace Client.WebUiHost.Game.Topics
{
    // ブロックの所持品と詳細をまとめて配信する
    // Combine block inventory contents and details for publication
    internal static class BlockInventoryDtoFactory
    {
        public static BlockInventoryDto Create(BlockSubInventorySource blockSource, SubInventoryModel sub, BlockGameObject block, BlockNetworkInfoCache networkCache)
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "block",
                BlockType = blockSource.BlockTypeName,
                BlockGuid = blockSource.BlockGuid.ToString("D"),
                Identifier = blockSource.BlockPosition.ToString(),
                ItemSlots = new List<BlockItemSlotDto>(sub.Count),
                FluidSlots = new List<BlockFluidSlotDto>(),
                Progress = null,
            };
            // SubInventory からスロットを写す（id/count は InventoryTopic 同型）
            // Copy slots from SubInventory; id/count mirrors InventoryTopic
            foreach (var stack in sub.SubInventory)
            {
                dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count });
            }
            // capability 詳細とネットワーク集約を充填する
            // Fill capability details and network aggregates
            BlockDetailDtoBuilder.Apply(dto, block, networkCache);
            return dto;
        }
    }
}
