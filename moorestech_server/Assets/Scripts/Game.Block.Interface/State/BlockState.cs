namespace Game.Block.Interface.State
{
    /// <summary>
    ///     変化したステートを通知するクラスです
    ///     <see cref="CurrentState" />や<see cref="PreviousState" />がStringなのは、ブロックの種類によって表現したいステートが異なり、
    ///     それらをパケットで取り扱う必要があるからです
    /// </summary>
    public struct BlockState
    {
        public readonly string CurrentState;
        public readonly string PreviousState;

        public readonly byte[] CurrentStateData;

        public BlockState(string currentState, string previousState, byte[] currentStateData = null)
        {
            CurrentState = currentState;
            PreviousState = previousState;
            CurrentStateData = currentStateData;
        }
    }
}