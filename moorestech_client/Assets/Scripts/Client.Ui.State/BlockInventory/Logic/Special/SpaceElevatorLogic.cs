using System;
using Game.Block.Interface.State;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Special
{
    public readonly struct SpaceElevatorStatusDisplay
    {
        public LocalizationKey LabelKey { get; }
        public bool Insufficient { get; }

        public SpaceElevatorStatusDisplay(LocalizationKey labelKey, bool insufficient)
        {
            LabelKey = labelKey;
            Insufficient = insufficient;
        }
    }

    /// <summary>
    /// 状態ごとの表示キーと、不足の色で出すか（Web spaceElevatorDisplay.ts の写し）
    /// Display key per status and whether it shows in the shortage color (port of Web spaceElevatorDisplay.ts)
    /// </summary>
    public static class SpaceElevatorLogic
    {
        public static SpaceElevatorStatusDisplay StatusDisplay(SpaceElevatorBaseStatus status)
        {
            return status switch
            {
                SpaceElevatorBaseStatus.ReadyToConnect => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusReadyToConnect, false),
                SpaceElevatorBaseStatus.InsufficientCable => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusInsufficientCable, true),
                SpaceElevatorBaseStatus.WaitingSatellite => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusWaitingSatellite, false),
                SpaceElevatorBaseStatus.Climbing => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusClimbing, false),
                SpaceElevatorBaseStatus.LowPower => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusLowPower, true),
                SpaceElevatorBaseStatus.InletBlocked => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusInletBlocked, true),
                SpaceElevatorBaseStatus.OutputBlocked => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusOutputBlocked, true),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }
    }
}
