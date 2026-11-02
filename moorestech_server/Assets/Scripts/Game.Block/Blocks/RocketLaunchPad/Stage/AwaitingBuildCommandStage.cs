using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class AwaitingBuildCommandStage : RocketLaunchPadStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.AwaitingBuildCommand;
        public static readonly AwaitingBuildCommandStage Instance = new();
        private AwaitingBuildCommandStage() { }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // silent-ok: 在庫・上限・初発射が変わるまで再検査不要 / UIは建造合図待ちを表示する
            if (!reevaluateWait) return;
            IdleStage.AdvanceWaitingForCargo(execution);
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution)
        {
            return StartBuilding(execution) switch
            {
                RocketLaunchPadWaitReason.OutputFull => RocketLaunchPadCommandResult.OutputFull,
                RocketLaunchPadWaitReason.MissingMaterials => RocketLaunchPadCommandResult.MissingMaterials,
                RocketLaunchPadWaitReason.MissingPayload => RocketLaunchPadCommandResult.MissingPayload,
                _ => RocketLaunchPadCommandResult.Accepted,
            };
        }

        internal static RocketLaunchPadWaitReason StartBuilding(RocketLaunchPadStageExecution execution)
        {
            if (!execution.CargoLoader.TryLoadNextCargo(out var cargo, out var reason)) return reason;
            execution.SetStage(new BuildingStage(cargo, execution.BuildTotalTicks));
            execution.SetWaitReason(RocketLaunchPadWaitReason.None);
            return RocketLaunchPadWaitReason.None;
        }
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) { } // silent-ok: この段階の初発射反応は不要 / UIは段階を維持し、建造待ちは次tickで再評価する
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => 0f;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => 0f;
    }
}
