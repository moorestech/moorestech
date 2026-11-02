using System;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class LaunchingStage : RocketLaunchPadStage, IProgressingStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Launching;
        public uint RemainingTicks { get; }
        public LaunchingStage(uint remainingTicks)
        {
            // 残り0は完了後の別段階で表すため進行段階には置けない
            // Zero belongs to the next completed stage and cannot remain in a progressing stage
            if (remainingTicks == 0) throw new ArgumentOutOfRangeException(nameof(remainingTicks), "進行段階は残りtickが1以上です");
            RemainingTicks = remainingTicks;
        }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // 上昇演出の期間だけ進め、到着処理を再実行しない
            // Advance only the ascent duration without replaying arrival
            execution.SetStage(1 < RemainingTicks ? new LaunchingStage(RemainingTicks - 1) : IdleStage.Instance);
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) { } // silent-ok: この段階の初発射反応は不要 / UIは段階を維持し、建造待ちは次tickで再評価する
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => 0f;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => Mathf.Clamp01(1f - RemainingTicks / (float)execution.LaunchTotalTicks);
    }
}
