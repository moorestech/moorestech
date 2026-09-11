namespace Game.SaveLoad.Interface
{
    // 即時スナップショットを要求する。受理されたかは戻り値の型が持ち、完了は ISnapshotWrittenNotifier で突き合わせる
    // Requests an immediate snapshot; the result type says whether it was accepted, and completion is matched via ISnapshotWrittenNotifier
    public interface ISnapshotCaptureRequest
    {
        SnapshotCaptureRequestResult RequestImmediateSnapshot();
    }
}
