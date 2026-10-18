namespace Core.BeltTransport
{
    // 1 tick を構成する段階。要素 index ごとの処理は互いに独立で、段階内はどの順でも・並列でも同じ結果になる
    // One stage of a tick; the work for each index is independent of the others, so a stage yields the same result in any order or in parallel
    // 段数は網の大きさによらず一定でなければならない(下流から順に更新する実装は段数が直列長に比例して増える)。ADR 0084 §3
    // The number of stages must not depend on the network size (an implementation that updates downstream first grows with the chain length). ADR 0084 §3
    public interface IBeltTickStage
    {
        int Count { get; }
        void Run(int index);
    }
}
