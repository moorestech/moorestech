using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Detail
{
    public sealed class MachineStateDisplay
    {
        public LocalizationKey LabelKey { get; }
        public bool Insufficient { get; }
        public bool ShowPowerRate { get; }

        public MachineStateDisplay(LocalizationKey labelKey, bool insufficient, bool showPowerRate)
        {
            LabelKey = labelKey;
            Insufficient = insufficient;
            ShowPowerRate = showPowerRate;
        }
    }

    public sealed class GearToElectricStateDisplay
    {
        public LocalizationKey LabelKey { get; }
        public bool Insufficient { get; }

        public GearToElectricStateDisplay(LocalizationKey labelKey, bool insufficient)
        {
            LabelKey = labelKey;
            Insufficient = insufficient;
        }
    }

    /// <summary>
    /// 状態ごとの表示（文言キー・不足トーン・充足率の表示可否）を 1 か所で決める。Web detailLogic.ts の表の写し
    /// Settles per-state display (label key, insufficient tone, rate visibility) in one place; a port of the tables in Web detailLogic.ts
    /// </summary>
    public static class BlockDetailStateDisplay
    {
        // uGUI GearEnergyTransformerUIView.GetStopReasonText と同文言。止まっていなければ文言なし
        // Same wording as uGUI GearEnergyTransformerUIView.GetStopReasonText; no text while not stopped
        public static bool TryGetStopReasonKey(GearNetworkStopReasonKind reason, out LocalizationKey key)
        {
            key = reason switch
            {
                GearNetworkStopReasonKind.None => default,
                GearNetworkStopReasonKind.Rocked => LocalizationKeys.Ui.BlockInventory.StopReasonLocked,
                GearNetworkStopReasonKind.OverRequirePower => LocalizationKeys.Ui.BlockInventory.StopReasonInsufficientPower,
                GearNetworkStopReasonKind.NoGeneration => LocalizationKeys.Ui.BlockInventory.StopReasonNoGeneration,
                GearNetworkStopReasonKind.NoGenerator => LocalizationKeys.Ui.BlockInventory.StopReasonNoGenerator,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
            return reason != GearNetworkStopReasonKind.None;
        }

        // halted は要求電力を出さないので充足率を出さず、不足トーンの文言だけにする
        // Halted requests no power, so the rate is hidden and only the insufficient-toned label remains
        public static MachineStateDisplay Machine(MachineProcessDisplayState state)
        {
            return state switch
            {
                MachineProcessDisplayState.Idle => new MachineStateDisplay(LocalizationKeys.Ui.BlockInventory.MachineStateIdle, false, true),
                MachineProcessDisplayState.Processing => new MachineStateDisplay(LocalizationKeys.Ui.BlockInventory.MachineStateProcessing, false, true),
                MachineProcessDisplayState.Halted => new MachineStateDisplay(LocalizationKeys.Ui.BlockInventory.MachineStateHalted, true, false),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }

        // 発電文言と不足トーンを 1 表で決める
        // Resolve the generation label and insufficient tone in one table
        public static GearToElectricStateDisplay GearToElectric(GearToElectricDisplayState state)
        {
            return state switch
            {
                GearToElectricDisplayState.Generating => new GearToElectricStateDisplay(LocalizationKeys.Ui.BlockInventory.GearToElectricStateGenerating, false),
                GearToElectricDisplayState.Idle => new GearToElectricStateDisplay(LocalizationKeys.Ui.BlockInventory.GearToElectricStateIdle, false),
                GearToElectricDisplayState.Stopped => new GearToElectricStateDisplay(LocalizationKeys.Ui.BlockInventory.GearToElectricStateStopped, true),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}
