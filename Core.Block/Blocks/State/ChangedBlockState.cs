namespace Core.Block.Blocks.State
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
        
        /// <summary>
        /// ブロックのステートのデータ
        /// 各ブロックのよって必要なデータは違うため、このクラスを継承して派生させる
        /// </summary>
        public readonly ChangeBlockStateData CurrentStateDetailInfo;
        
        public ChangedBlockState(string currentState, string previousState, ChangeBlockStateData currentStateDetailInfo = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateDetailInfo = currentStateDetailInfo;
        }
    }
}