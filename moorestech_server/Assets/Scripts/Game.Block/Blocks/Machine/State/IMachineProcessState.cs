namespace Game.Block.Blocks.Machine.State
{
    // 各加工ステートの共通インターフェース
    // Common interface for each processing state
    internal interface IMachineProcessState
    {
        ProcessState State { get; }

        // ステートへ入った瞬間の処理
        // Runs the moment the state is entered
        void OnEnter();

        // 毎tickの更新。遷移先ステートを返す（同じ値なら滞在）
        // Per-tick update; returns the next state (same value means stay)
        ProcessState GetNextUpdate();

        // ステートから出る瞬間の処理
        // Runs the moment the state is exited
        void OnExit();
    }
}
