namespace Game.Block.Interface.State
{
    /// <summary>
    /// 変化したステートを通知するクラスです
    /// <see cref="CurrentState"/>や<see cref="PreviousState"/>がStringなのは、ブロックの種類によって表現したいステートが異なり、
    /// それらをパケットで取り扱う必要があるからです
    /// TODO シリアライズ可能なクラスにした方がいいかも？
    /// </summary>
    public class ChangedBlockState
    {
        public readonly string CurrentState;
        public readonly string PreviousState;
        public readonly string CurrentStateJsonData;

        public ChangedBlockState(string currentState, string previousState, string currentStateJsonData = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateJsonData = currentStateJsonData;
        }
    }
}