using System;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.RocketLaunchPad.Stage
{
    // 段階ごとの振る舞いを必須にして追加時の実装漏れを防ぐ
    // Require behavior on every stage so additions cannot omit an operation
    public abstract class RocketLaunchPadStage
    {
        public abstract RocketLaunchPadPhase Phase { get; }
        internal abstract void Advance(RocketLaunchPadStageExecution execution, float tickPower, bool reevaluateWait);
        internal abstract RocketLaunchPadCommandResult RequestBuild(RocketLaunchPadStageExecution execution);
        internal abstract RocketLaunchPadCommandResult RequestLaunch(RocketLaunchPadStageExecution execution);
        internal abstract void OnFirstLaunch(RocketLaunchPadStageExecution execution);
        internal abstract float GetRequestPower(RocketLaunchPadStageExecution execution);
        internal abstract float CalculateProgress(RocketLaunchPadStageExecution execution);
    }

    // 積荷は積載段階だけ、残り時間は進行段階だけが保持する
    // Only loaded stages hold cargo, and only progressing stages hold remaining time
    public abstract class LoadedRocketStage : RocketLaunchPadStage
    {
        public readonly RocketCargo Cargo;
        protected LoadedRocketStage(RocketCargo cargo)
        {
            if (!cargo.IsLoaded) throw new ArgumentException("積荷の無いロケットで積載中の段階を作ろうとしました", nameof(cargo));
            Cargo = cargo;
        }
    }

    public interface IProgressingStage
    {
        uint RemainingTicks { get; }
    }
}
