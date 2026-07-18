using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Game.InGame.UI.UIState.State.SubInventory;

namespace Client.WebUiHost.Game.Topics
{
    // 統一SubInventoryの列車状態をWeb向けDTOへ変換する
    // Converts unified train SubInventory state into its Web DTO.
    public static class TrainInventoryDtoFactory
    {
        public static BlockInventoryDto Create(TrainSubInventorySource source, ISubInventory inventory)
        {
            var dto = new BlockInventoryDto
            {
                Open = true,
                Source = "train",
                Identifier = source.TrainCarInstanceId.ToString(),
                BlockName = "Train Inventory",
                BlockType = "Train",
                ItemSlots = new List<BlockItemSlotDto>(inventory.Count),
                FluidSlots = new List<BlockFluidSlotDto>(),
                Error = ResolveError(inventory),
            };
            foreach (var stack in inventory.SubInventory)
            {
                dto.ItemSlots.Add(new BlockItemSlotDto { ItemId = stack.Id.AsPrimitive(), Count = stack.Count });
            }
            return dto;
        }

        private static string ResolveError(ISubInventory inventory)
        {
            if (inventory is not ITrainInventoryView trainView || trainView.CurrentMessageType == null) return null;
            return trainView.CurrentMessageType.Value switch
            {
                TrainInventoryMessageType.ContainerMissing => "containerMissing",
                TrainInventoryMessageType.TrainCarMissing => "trainCarMissing",
                _ => "openFailed",
            };
        }
    }
}
