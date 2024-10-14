using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Interface.State
{
    /// <summary>
    ///     変化したステートを通知するクラスです
    ///     <see cref="CurrentState" />や<see cref="PreviousState" />がStringなのは、ブロックの種類によって表現したいステートが異なり、
    ///     それらをパケットで取り扱う必要があるからです
    /// </summary>
    public class BlockState
    {
        public readonly string CurrentState;
        public readonly string PreviousState;
        
        public readonly Dictionary<string, byte[]> CurrentStateDetail;
        
        public BlockState(string currentState, string previousState, Dictionary<string, byte[]> currentStateDetail = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateDetail = currentStateDetail ?? new Dictionary<string, byte[]>();
        }
        
        public BlockState(BlockStateTypes blockStateTypes, Dictionary<string, byte[]> currentStateDetail)
        {
            CurrentState = blockStateTypes.CurrentStateType;
            PreviousState = blockStateTypes.PreviousStateType;
            CurrentStateDetail = currentStateDetail;
        }
    }
}