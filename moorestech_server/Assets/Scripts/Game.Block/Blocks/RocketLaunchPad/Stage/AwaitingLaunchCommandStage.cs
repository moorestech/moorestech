using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class AwaitingLaunchCommandStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingLaunchCommand;
        public AwaitingLaunchCommandStage(RocketCargo cargo) : base(cargo) { }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // silent-ok: 合図か初発射通知まで進行しない / UIは発射合図待ちを表示する
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution)
        {
            execution.SetStage(new ReadyToLaunchStage(Cargo));
            return RocketLaunchPadCommandResult.Accepted;
        }
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) => execution.SetStage(new ReadyToLaunchStage(Cargo));
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => 0f;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => 1f;
    }
}
