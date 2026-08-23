using System;

namespace Game.SaveLoad.Interface
{
    // 保存の書き出し完了を通知する。終了時のflush待ちが購読する
    // Notifies that a save finished writing; the shutdown flush wait subscribes to it
    public interface IWorldSaveCompletionNotifier
    {
        // 書き出しが完了した要求番号を流す
        // Emits the generation whose write has completed
        IObservable<long> OnWorldSaveCompleted { get; }
    }
}
