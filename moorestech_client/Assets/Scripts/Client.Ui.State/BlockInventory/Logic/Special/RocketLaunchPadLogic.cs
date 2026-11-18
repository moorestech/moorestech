using System;
using Game.Block.Interface.State;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Special
{
    // 段階と待ち理由はサーバーの Game.Block.Interface.State の enum をそのまま使う（判断記録 D-1）
    // Phase and wait reason reuse the server enums in Game.Block.Interface.State as they are (D-1)
    public enum RocketCommand
    {
        Build,
        Launch,
    }

    public readonly struct RocketCommandButton
    {
        public RocketCommand Command { get; }
        public LocalizationKey LabelKey { get; }

        public RocketCommandButton(RocketCommand command, LocalizationKey labelKey)
        {
            Command = command;
            LabelKey = labelKey;
        }
    }

    /// <summary>
    /// 発射台の状態文と合図ボタン（Web rocketLaunchPadLogic.ts の写し）
    /// The launch pad's status text and command button (port of Web rocketLaunchPadLogic.ts)
    /// </summary>
    public static class RocketLaunchPadLogic
    {
        // 待ち理由があれば段階より優先する
        // A wait reason, when present, takes precedence over the phase
        public static LocalizationKey StatusKey(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason)
        {
            return waitReason switch
            {
                RocketLaunchPadWaitReason.None => PhaseStatusKey(phase),
                RocketLaunchPadWaitReason.MissingMaterials => LocalizationKeys.Ui.BlockInventory.RocketWaitMissingMaterials,
                RocketLaunchPadWaitReason.MissingPayload => LocalizationKeys.Ui.BlockInventory.RocketWaitMissingPayload,
                RocketLaunchPadWaitReason.OutputFull => LocalizationKeys.Ui.BlockInventory.RocketWaitOutputFull,
                RocketLaunchPadWaitReason.NoPower => LocalizationKeys.Ui.BlockInventory.RocketWaitNoPower,
                _ => throw new ArgumentOutOfRangeException(nameof(waitReason), waitReason, null),
            };

            #region Internal

            LocalizationKey PhaseStatusKey(RocketLaunchPadPhase phase)
            {
                return phase switch
                {
                    RocketLaunchPadPhase.Idle => LocalizationKeys.Ui.BlockInventory.RocketStatusIdle,
                    RocketLaunchPadPhase.AwaitingBuildCommand => LocalizationKeys.Ui.BlockInventory.RocketStatusAwaitingBuild,
                    RocketLaunchPadPhase.Building => LocalizationKeys.Ui.BlockInventory.RocketStatusBuilding,
                    RocketLaunchPadPhase.AwaitingLaunchCommand => LocalizationKeys.Ui.BlockInventory.RocketStatusAwaitingLaunch,
                    RocketLaunchPadPhase.ReadyToLaunch => LocalizationKeys.Ui.BlockInventory.RocketStatusReadyToLaunch,
                    RocketLaunchPadPhase.Launching => LocalizationKeys.Ui.BlockInventory.RocketStatusLaunching,
                    _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
                };
            }

            #endregion
        }

        // 合図のボタンは初打ち上げ前の合図待ちだけに出る
        // Command buttons appear only while awaiting a command before the first launch
        public static bool TryGetCommandButton(RocketLaunchPadPhase phase, out RocketCommandButton button)
        {
            switch (phase)
            {
                case RocketLaunchPadPhase.AwaitingBuildCommand:
                    button = new RocketCommandButton(RocketCommand.Build, LocalizationKeys.Ui.BlockInventory.RocketBuild);
                    return true;
                case RocketLaunchPadPhase.AwaitingLaunchCommand:
                    button = new RocketCommandButton(RocketCommand.Launch, LocalizationKeys.Ui.BlockInventory.RocketLaunch);
                    return true;
                default:
                    button = default;
                    return false;
            }
        }
    }
}
