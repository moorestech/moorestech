using System;

namespace Game.SaveLoad.Interface
{
    // スナップショットの書き出し完了を通知する。通常はtickスレッド、終了時の待ち合わせ経由では待ち合わせスレッドから発火する
    // Notifies that a snapshot finished writing; normally on the tick thread, and on the waiting thread when it comes through the shutdown wait
    public interface ISnapshotWrittenNotifier
    {
        IObservable<SnapshotWritten> OnSnapshotWritten { get; }
    }
}
