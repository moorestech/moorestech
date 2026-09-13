namespace Game.SaveLoad.Interface
{
    /// <summary>ロード時にマスタ欠損で取り除いた件数。プレイヤーへの1回きりの知らせに使う</summary>
    /// <summary>How much was removed for missing master data at load; drives the one-time player notice</summary>
    public sealed class MissingMasterPruneReport
    {
        public int RemovedBlockCount { get; }
        public int EmptiedItemStackCount { get; }
        public int RemovedResearchCount { get; }

        public MissingMasterPruneReport(int removedBlockCount, int emptiedItemStackCount, int removedResearchCount)
        {
            RemovedBlockCount = removedBlockCount;
            EmptiedItemStackCount = emptiedItemStackCount;
            RemovedResearchCount = removedResearchCount;
        }

        public bool HasRemoval => RemovedBlockCount > 0 || EmptiedItemStackCount > 0 || RemovedResearchCount > 0;

        public static MissingMasterPruneReport None { get; } = new(0, 0, 0);
    }
}
