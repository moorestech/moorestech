using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// block_inventory.current の capability 詳細 DTO（spec D1: 機能単位合成）
    /// Capability detail DTOs for block_inventory.current (spec D1: per-feature composition)
    /// </summary>
    public class MachineDetailDto
    {
        public string RecipeGuid;
        public string CurrentState;
        public float CurrentPower;
        public float RequestPower;
        public SlotLayoutDto SlotLayout;
    }

    public class SlotLayoutDto
    {
        public int Input;
        public int Output;
        public int Module;
    }

    public class GeneratorDetailDto
    {
        public double RemainingFuelTime;
        public double CurrentFuelTime;
        public float OperatingRate;
    }

    public class MinerDetailDto
    {
        public float CurrentPower;
        public float RequestPower;
        public List<MiningItemDto> MiningItems;
    }

    public class MiningItemDto
    {
        public int ItemId;
        public float ItemsPerMinute;
    }

    public class GearDetailDto
    {
        public bool IsClockwise;
        public float CurrentRpm;
        public float CurrentTorque;
        public float BaseRpm;
        public float BaseTorque;
    }

    public class ElectricNetworkDto
    {
        public float TotalGeneratePower;
        public float TotalRequiredPower;
        public int ConsumerCount;
        public float PowerRate;
    }

    public class GearNetworkDto
    {
        public float TotalRequiredGearPower;
        public float TotalGenerateGearPower;
        public string StopReason;
    }

    public class FilterSplitterDto
    {
        public int DirectionCount;
        public int FilterSlotCountPerDirection;
        public List<FilterSplitterDirectionDto> Directions;
    }

    public class FilterSplitterDirectionDto
    {
        public string Mode;
        public List<int> FilterItemIds;
    }

    public class ElectricToGearDetailDto
    {
        public int SelectedIndex;
        public float FulfillmentRate;
        public float ConsumedElectricPower;
        public List<ElectricToGearOutputModeDto> OutputModes;
    }

    public class ElectricToGearOutputModeDto
    {
        public double Rpm;
        public double Torque;
        public double RequiredPower;
    }

    public class TrainPlatformDetailDto
    {
        public string Mode;
        public int? ItemSlotCount;
        public double? FluidCapacity;
    }
}
