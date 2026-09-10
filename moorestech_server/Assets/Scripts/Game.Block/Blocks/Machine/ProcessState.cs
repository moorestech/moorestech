using System;

namespace Game.Block.Blocks.Machine
{
    public static class ProcessStateExtension
    {
        /// <summary>
        ///     ProcessStateを文字列に変換
        ///     EnumのToStringを使わない理由はアロケーションによる速度低下をなくすためです。
        /// </summary>
        public static string ToStr(this ProcessState state)
        {
            return state switch
            {
                ProcessState.Idle => VanillaMachineBlockStateConst.IdleState,
                ProcessState.Processing => VanillaMachineBlockStateConst.ProcessingState,
                ProcessState.Halted => VanillaMachineBlockStateConst.HaltedState,
                // 出力詰まりはプレイヤーから見れば手が止まった待機なので、公開状態はidleと同じにする
                // Output blockage looks like a stopped machine to the player, so it publishes the same idle state
                ProcessState.OutputBlocked => VanillaMachineBlockStateConst.IdleState,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }

    public enum ProcessState
    {
        Idle,
        Processing,
        Halted,

        // 加工tickは尽きたが産出物が出力先に収まらず完了を保留している状態。要求電力は待機と同じ
        // Ticks are exhausted but the outputs do not fit, so completion is held; the power request matches idle
        OutputBlocked,
    }
}
