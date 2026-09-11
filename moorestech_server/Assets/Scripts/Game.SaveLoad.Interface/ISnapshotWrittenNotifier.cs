using System;

namespace Game.SaveLoad.Interface
{
    // スナップショットの書き出し完了を通知する。tickスレッド上で発火する
    // Notifies that a snapshot finished writing; fired on the tick thread
    public interface ISnapshotWrittenNotifier
    {
        IObservable<SnapshotWritten> OnSnapshotWritten { get; }
    }
}
