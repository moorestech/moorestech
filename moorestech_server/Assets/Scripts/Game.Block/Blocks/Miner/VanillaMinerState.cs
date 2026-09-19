using System;

namespace Game.Block.Blocks.Miner
{
    public enum VanillaMinerState
    {
        Idle,
        Mining,
    }
    
    public static class ProcessStateExtension
    {
        /// <summary>
        ///     <see cref="ProcessState" />をStringに変換します。
        ///     EnumのToStringを使わない理由はアロケーションによる速度低下をなくすためです。
        /// </summary>
        public static string ToStr(this VanillaMinerState state)
        {
            return state switch
            {
                VanillaMinerState.Idle => "idle",
                VanillaMinerState.Mining =>"mining",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}
