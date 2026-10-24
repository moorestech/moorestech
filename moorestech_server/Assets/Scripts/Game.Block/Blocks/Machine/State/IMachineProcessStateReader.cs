namespace Game.Block.Blocks.Machine.State
{
    /// <summary>
    ///     機械の加工状態を読む口。電気・歯車機械と清浄室機械の2つの processor が既存の CurrentState で実装する
    ///     Read port for a machine's process state, implemented by both the electric/gear and the clean-room processors through their existing CurrentState
    /// </summary>
    public interface IMachineProcessStateReader
    {
        ProcessState CurrentState { get; }
    }
}
