using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Special
{
    public enum SpaceElevatorStatus
    {
        ReadyToConnect,
        InsufficientCable,
        WaitingSatellite,
        Climbing,
        LowPower,
        InletBlocked,
        OutputBlocked,
    }

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
        public static SpaceElevatorStatusDisplay StatusDisplay(SpaceElevatorStatus status)
        {
            return status switch
            {
                SpaceElevatorStatus.ReadyToConnect => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusReadyToConnect, false),
                SpaceElevatorStatus.InsufficientCable => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusInsufficientCable, true),
                SpaceElevatorStatus.WaitingSatellite => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusWaitingSatellite, false),
                SpaceElevatorStatus.Climbing => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusClimbing, false),
                SpaceElevatorStatus.LowPower => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusLowPower, true),
                SpaceElevatorStatus.InletBlocked => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusInletBlocked, true),
                SpaceElevatorStatus.OutputBlocked => new SpaceElevatorStatusDisplay(LocalizationKeys.Ui.BlockInventory.SpaceElevatorStatusOutputBlocked, true),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }
    }
}
