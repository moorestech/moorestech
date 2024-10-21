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
        public readonly Dictionary<string, byte[]> CurrentStateDetails;
        
        public BlockState(Dictionary<string, byte[]> currentStateDetails)
        {
            CurrentStateDetails = currentStateDetails;
        }
    }
}