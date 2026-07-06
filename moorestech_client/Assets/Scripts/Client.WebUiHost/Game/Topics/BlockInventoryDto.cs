using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// block_inventory.current の配信 DTO
    /// Payload DTO for block_inventory.current
    /// </summary>
    public class BlockInventoryDto
    {
        public bool Open;
        public string BlockType;
        public string Identifier;
        public string BlockName;
        public List<BlockItemSlotDto> ItemSlots;
        public List<BlockFluidSlotDto> FluidSlots;
        public double? Progress;
    }

    public class BlockItemSlotDto
    {
        public int ItemId;
        public int Count;
    }

    public class BlockFluidSlotDto
    {
        public int FluidId;
        public double Amount;
        public double Capacity;
        public string Name;
    }
}
