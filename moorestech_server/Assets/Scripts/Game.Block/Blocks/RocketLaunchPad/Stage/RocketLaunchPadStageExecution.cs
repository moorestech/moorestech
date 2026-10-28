using Core.Update;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    // 段階の実行に必要な依存と現在段階を1か所で保持する
    // Hold stage execution dependencies and the current stage in one place
    internal sealed class RocketLaunchPadStageExecution
    {
        internal readonly RocketLaunchPadCargoLoader CargoLoader;
        internal readonly RocketLaunchPadOutputSlots OutputSlots;
        internal readonly RocketLaunchHistory LaunchHistory;
        internal readonly float RequiredPower;
        internal readonly uint BuildTotalTicks;
        internal readonly uint LaunchTotalTicks;
        internal RocketLaunchPadStage Stage { get; private set; } = IdleStage.Instance;
        internal RocketLaunchPadWaitReason WaitReason { get; private set; }

        internal RocketLaunchPadStageExecution(RocketLaunchPadBlockParam param, RocketLaunchPadCargoLoader cargoLoader, RocketLaunchPadOutputSlots outputSlots, RocketLaunchHistory launchHistory)
        {
            CargoLoader = cargoLoader;
            OutputSlots = outputSlots;
            LaunchHistory = launchHistory;
            RequiredPower = param.RequiredPower;
            BuildTotalTicks = GameUpdater.SecondsToTicks(param.BuildSeconds);
            LaunchTotalTicks = GameUpdater.SecondsToTicks(param.LaunchSeconds);
        }

        internal void SetStage(RocketLaunchPadStage stage) => Stage = stage;
        internal void SetWaitReason(RocketLaunchPadWaitReason reason) => WaitReason = reason;
    }
}
