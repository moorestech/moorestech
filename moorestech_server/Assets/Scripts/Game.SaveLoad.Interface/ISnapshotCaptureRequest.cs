namespace Game.SaveLoad.Interface
{
    // 即時スナップショットを要求し要求IDを返す。完了は ISnapshotWrittenNotifier で突き合わせる
    // Requests an immediate snapshot and returns its id; completion is matched via ISnapshotWrittenNotifier
    public interface ISnapshotCaptureRequest
    {
        long RequestImmediateSnapshot();
    }
}
