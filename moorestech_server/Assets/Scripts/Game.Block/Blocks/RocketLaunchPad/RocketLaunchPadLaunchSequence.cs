using System;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 段階へ処理を委譲し、変化時だけ待機条件を再評価する
    // Delegate behavior to stages and reevaluate waiting conditions only after changes
    internal class RocketLaunchPadLaunchSequence
    {
        public RocketLaunchPadStage Stage => _execution.Stage;
        public RocketLaunchPadWaitReason WaitReason => _execution.WaitReason;
        public float RequestPower => Stage.GetRequestPower(_execution);
        private readonly RocketLaunchPadStageExecution _execution;
        private ulong _waitGeneration = 1;
        private ulong _processedWaitGeneration;

        public RocketLaunchPadLaunchSequence(RocketLaunchPadBlockParam param, RocketLaunchPadCargoLoader cargoLoader, RocketLaunchPadOutputSlots outputSlots, IOrbitalArrivalService arrivalService, RocketLaunchHistory launchHistory)
        {
            _execution = new RocketLaunchPadStageExecution(param, cargoLoader, outputSlots, arrivalService, launchHistory);
        }

        public void SetStage(RocketLaunchPadStage stage)
        {
            if (!ReferenceEquals(Stage, stage)) InvalidateWait();
            _execution.SetStage(stage);
        }

        public void InvalidateWait() => _waitGeneration++;

        public void Advance(float tickPower)
        {
            // 正常復帰した開始世代だけを処理済みにする
            // Acknowledge only the starting generation after normal completion
            var previous = Stage;
            var generation = _waitGeneration;
            Stage.Advance(_execution, tickPower, generation != _processedWaitGeneration);
            _processedWaitGeneration = generation;
            if (!ReferenceEquals(previous, Stage)) InvalidateWait();
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            var result = Stage.RequestBuild(_execution);
            if (result == RocketLaunchPadCommandResult.Accepted) InvalidateWait();
            return result;
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            var result = Stage.RequestLaunch(_execution);
            if (result == RocketLaunchPadCommandResult.Accepted) InvalidateWait();
            return result;
        }

        public void ReleaseLaunchCommandWait()
        {
            // 初発射で建造待ちも次tickに再評価する。発射自体は段階のtick口だけ
            // Reevaluate build waits next tick after the first launch; actual launch remains in the stage tick operation
            InvalidateWait();
            Stage.OnFirstLaunch(_execution);
        }

        public float CalculateProgress() => Stage.CalculateProgress(_execution);
    }
}
