using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class IdleStage : RocketLaunchPadStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.Idle;
        public static readonly IdleStage Instance = new();
        private IdleStage() { }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // silent-ok: 在庫・上限・初発射が変わるまで再検査不要 / UIは現在の待ち理由を表示する
            if (!reevaluateWait) return;
            AdvanceWaitingForCargo(execution);
        }

        internal static void AdvanceWaitingForCargo(RocketLaunchPadStageExecution execution)
        {
            // 初打ち上げ前は非消費で評価し、自動運転では選択と積載をまとめる
            // Evaluate without consumption before the first launch; automatic operation selects and loads together
            var reason = execution.LaunchHistory.HasLaunchedOnce
                ? AwaitingBuildCommandStage.StartBuilding(execution) : execution.CargoLoader.GetLoadWaitReason();
            execution.SetWaitReason(reason);
            if (reason != RocketLaunchPadWaitReason.None) execution.SetStage(Instance);
            else if (!execution.LaunchHistory.HasLaunchedOnce) execution.SetStage(AwaitingBuildCommandStage.Instance);
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) { } // silent-ok: この段階の初発射反応は不要 / UIは段階を維持し、建造待ちは次tickで再評価する
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => 0f;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => 0f;
    }
}
