using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.Master;
using Game.Block.Blocks.ElectricToGear;
using Game.Block.Blocks.GearToElectric;
using Game.Block.Interface.State;
using Game.Gear.Common;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// state detail とマスタの値から各詳細を組む（Web の ElectricToGear/GearToElectric/Gear/RocketLaunchPad/SpaceElevator の DtoBuilder の写し）
    /// Builds each detail from state details and master values (port of the Web DtoBuilders)
    /// </summary>
    public static class SpecialStateDetailReader
    {
        public static ElectricToGearDetail ElectricToGear(ElectricToGearGeneratorBlockStateDetail state, IReadOnlyList<ElectricToGearOutputMode> outputModes)
        {
            return new ElectricToGearDetail(outputModes, state.SelectedIndex, state.ElectricFulfillmentRate, state.ConsumedElectricPower);
        }

        public static GearToElectricDetail GearToElectric(GearToElectricGeneratorBlockStateDetail state, double maxGeneratedPower)
        {
            return new GearToElectricDetail(ToDisplayState(state.GenerationState), state.GeneratedElectricPower, maxGeneratedPower);
        }

        private static GearToElectricDisplayState ToDisplayState(GearToElectricGenerationState state)
        {
            return state switch
            {
                GearToElectricGenerationState.Generating => GearToElectricDisplayState.Generating,
                GearToElectricGenerationState.Idle => GearToElectricDisplayState.Idle,
                GearToElectricGenerationState.Stopped => GearToElectricDisplayState.Stopped,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "未知の回転発電機の状態"),
            };
        }

        public static GearDetail Generator(GearStateDetail gear)
        {
            return GearDetail.Generator(gear.CurrentRpm, gear.CurrentTorque);
        }

        public static GearDetail Consumer(GearStateDetail gear, double baseRpm)
        {
            return GearDetail.Consumer(gear.CurrentRpm, gear.CurrentTorque, baseRpm);
        }

        public static RocketLaunchPadDetail RocketLaunchPad(RocketLaunchPadStateDetail state, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, IReadOnlyList<InletSlotBinding> inletBindings, int outputSlotStart)
        {
            return new RocketLaunchPadDetail(state.Phase, state.WaitReason, state.Progress, materialSlots, payloadSlots, inletBindings, outputSlotStart);
        }

        // 入口は 1 枠で運べる積荷だけを受け、出口は先頭から連番（Web SpaceElevatorDetailDtoBuilder と同じ）
        // One inlet slot accepting the carried payloads and consecutive outlets from the first output slot (same as the Web builder)
        public static SpaceElevatorDetail SpaceElevator(SpaceElevatorBaseStateDetail state, int satelliteSlot, IReadOnlyList<ItemId> carriedPayloadItemIds, int firstOutputSlot, int outputSlotCount, int cablesPerElevator)
        {
            var outputs = new List<int>(outputSlotCount);
            for (var i = 0; i < outputSlotCount; i++) outputs.Add(firstOutputSlot + i);
            var inletBindings = new[] { new InletSlotBinding(satelliteSlot, carriedPayloadItemIds) };
            return new SpaceElevatorDetail(new[] { satelliteSlot }, outputs, inletBindings, state.IsConnected, state.CablePoolAmount, cablesPerElevator, state.ClimbProgress, state.Status);
        }
    }
}
