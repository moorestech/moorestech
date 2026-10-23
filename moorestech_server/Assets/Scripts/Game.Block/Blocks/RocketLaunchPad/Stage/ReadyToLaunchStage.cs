using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    public sealed class ReadyToLaunchStage : LoadedRocketStage
    {
        public override RocketLaunchPadPhase Phase => RocketLaunchPadPhase.ReadyToLaunch;
        public ReadyToLaunchStage(RocketCargo cargo) : base(cargo) { }

        internal override void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait)
        {
            // silent-ok: 出力口の空きが変わるまで再検査不要 / UIは出力満杯を表示する
            if (!reevaluateWait) return;
            var payload = Cargo.Payload;
            if (!execution.OutputSlots.CanStore(execution.ArrivalService.PreviewArrivalOutputs(payload.Id, payload.Count)))
            {
                execution.SetWaitReason(RocketLaunchPadWaitReason.OutputFull);
                return; // silent-ok: 出力口の空き待ちは通常の待機 / UIの待ち理由でプレイヤーに伝える
            }

            // 発射開始はこのtick口だけ。通知時にはLaunchingを観測できる順序を保つ
            // This tick operation is the sole launch entry; preserve Launching as the stage observed by arrival subscribers
            execution.SetWaitReason(RocketLaunchPadWaitReason.None);
            execution.SetStage(new LaunchingStage(execution.LaunchTotalTicks));
            execution.OutputSlots.Store(execution.ArrivalService.Arrive(payload.Id, payload.Count));
            execution.LaunchHistory.RecordLaunch();
        }

        // 不適合な合図は呼出側が理由付きで通知する
        // The caller reports incompatible commands with their reasons
        internal override RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingBuildCommand;
        internal override RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution) => RocketLaunchPadCommandResult.NotAwaitingLaunchCommand;
        internal override void OnFirstLaunch(RocketLaunchPadStageExecution execution) { } // silent-ok: この段階の初発射反応は不要 / UIは段階を維持し、建造待ちは次tickで再評価する
        internal override float GetRequestPower(RocketLaunchPadStageExecution execution) => 0f;
        internal override float CalculateProgress(RocketLaunchPadStageExecution execution) => 1f;
    }
}
