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

        // ユーザー選択レシピと対象ブロックを Web UI の選択表示・絞り込みへ渡す
        // Expose the selected recipe and target block for Web UI selection and filtering
        public string SelectedRecipeGuid;
        public string BlockGuid;

        public double RecipeTime;
        public List<MachineOutputItemDto> OutputItems;
        public string CurrentState;
        public float CurrentPower;
        public float RequestPower;
        public SlotLayoutDto SlotLayout;

        // 選択レシピが束縛するスロット・タンクの正本。Web側は束縛規則を再導出せずこれを読むだけにする
        // The authoritative slot/tank binding of the selected recipe; the Web side reads it instead of re-deriving the rule
        public List<MachineSlotBindingDto> SlotBindings;
        public List<MachineTankBindingDto> TankBindings;
    }

    public class MachineSlotBindingDto
    {
        // 統合スロット番号（入力→出力→モジュールの連結順）
        // Unified slot index (inputs, then outputs, then modules)
        public int Slot;
        public int ItemId;
        public int Count;
    }

    public class MachineTankBindingDto
    {
        // 液体行index（入力タンク→出力タンクの連結順）
        // Fluid row index (input tanks, then output tanks)
        public int Tank;
        public string FluidGuid;
        public double Amount;
    }

    public class MachineOutputItemDto
    {
        public int ItemId;
        public int Count;
    }

    public class SlotLayoutDto
    {
        public int Input;
        public int Output;
        public int Module;

        // Web側のゴースト導出が液体表示indexを実タンク数へクランプするために必要
        // Needed by the Web-side ghost derivation to clamp fluid display indices to the real tank count
        public int InputTank;
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

    public class PumpDetailDto
    {
        // 油井のみ設定。歯車はnull(GearSection使用)
        // Only the electric pump sets this; gear pump is null (uses GearSection)
        public PumpElectricDto Electric;
        public List<PumpingFluidDto> PumpingFluids;
    }

    public class PumpElectricDto
    {
        public string CurrentState;
        public float CurrentPower;
        public float RequestPower;
    }

    public class PumpingFluidDto
    {
        public int FluidId;
        public string FluidGuid;
        public float AmountPerMinute;
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
