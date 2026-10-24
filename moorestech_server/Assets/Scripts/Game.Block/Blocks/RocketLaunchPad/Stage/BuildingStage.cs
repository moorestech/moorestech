using System;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Block.Blocks.Util;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class BuildingStage : LoadedRocketStage, IProgressingStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Building;
        public uint RemainingTicks { get; }
        public BuildingStage(RocketCargo cargo, uint remainingTicks) : base(cargo)
        {
            // 残り0は完了後の別段階で表すため進行段階には置けない
            // Zero belongs to the next completed stage and cannot remain in a progressing stage
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "進行段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // 電力の供給率に応じたtickだけを消費する
            // Consume ticks in proportion to supplied power
            execution.SetWaitReason(tickPower <= 0f ? RocketLaunchPadWaitReason.NoPower : RocketLaunchPadWaitReason.None);
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(tickPower, execution.RequiredPower);
            if (subTicks < RemainingTicks)
            {
                execution.SetStage(new BuildingStage(Cargo, RemainingTicks - subTicks));
                return;
            }

            // 初打ち上げ前だけ完成後に手動の発射合図を待つ
            // Only before the first launch does completion wait for a manual launch command
            execution.SetWaitReason(RocketLaunchPadWaitReason.None);
            execution.SetStage(execution.LaunchHistory.HasLaunchedOnce ? new ReadyToLaunchStage(Cargo) : new AwaitingLaunchCommandStage(Cargo));
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) { } // silent-ok: この段階の初発射反応は不要 / UIは段階を維持し、建造待ちは次tickで再評価する
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => execution.RequiredPower;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => Mathf.Clamp01(1f - RemainingTicks / (float)execution.BuildTotalTicks);
    }
}
