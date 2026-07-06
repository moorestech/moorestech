using System.Collections.Generic;
using Client.WebUiHost.Game.Topics.BlockDetail;

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
        // capability 詳細（該当ブロックのみ。null はキー省略される）
        // Capability details (only for applicable blocks; null keys are omitted)
        public MachineDetailDto Machine;
        public GeneratorDetailDto Generator;
        public MinerDetailDto Miner;
        public GearDetailDto Gear;
        public ElectricNetworkDto ElectricNetwork;
        public GearNetworkDto GearNetwork;
        public FilterSplitterDto FilterSplitter;
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
