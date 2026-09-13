namespace Game.SaveLoad.Snapshot
{
    // 取り込みの出自。周期と即時で保持の責務が違うため、世代リストはこれで分かれる
    // Where a capture came from; periodic and immediate have different retention duties, so the generation lists split on this
    public enum SnapshotCaptureKind
    {
        Periodic = 0,
        Immediate = 1,
    }
}
