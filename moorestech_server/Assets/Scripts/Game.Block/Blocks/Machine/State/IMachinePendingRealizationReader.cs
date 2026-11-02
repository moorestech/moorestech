namespace Game.Block.Blocks.Machine.State
{
    /// <summary>
    ///     出力の空きを待っている確定済みの加工結果を読む口。待機ステートが実装し、表示側が確定時の値を読む
    ///     Read port for the realized job waiting for output space; the idle state implements it and displays read the values fixed at realization
    /// </summary>
    internal interface IMachinePendingRealizationReader
    {
        bool TryGetPendingRealization(out MachineRealization realization);
    }
}
