using System;
using System.IO;
using System.Threading;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using UniRx;

namespace Game.SaveLoad
{
    public sealed class WorldSaveCoordinator : IWorldSaveRequest, IWorldSaveCompletionNotifier
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly WorldDataDirectory _worldDataDirectory;
        private readonly Subject<long> _onWorldSaveCompleted = new();
        private long _requestedGeneration;
        private long _completedGeneration;

        public WorldSaveCoordinator(WorldDataDirectory worldDataDirectory, AssembleSaveJsonText assembleSaveJsonText)
        {
            _worldDataDirectory = worldDataDirectory;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        // 要求済みだがまだ書き出していない保存が残っているか。終了時の待ち合わせに使う
        // Whether a requested save is still unwritten; used to wait for the flush at shutdown
        public bool HasPendingSave => Volatile.Read(ref _requestedGeneration) != Volatile.Read(ref _completedGeneration);

        // 書き出しが完了した要求番号を流す。終了時のflush待ちがこれで完了を判定する
        // Emits the generation whose write completed; the shutdown flush wait decides completion from it
        public IObservable<long> OnWorldSaveCompleted => _onWorldSaveCompleted;

        public long RequestSave()
        {
            return Interlocked.Increment(ref _requestedGeneration);
        }

        public void SaveIfRequested()
        {
            // このtickで処理する要求番号を固定し、保存中に届く要求を次回へ残す
            // Freeze the generation handled now so requests arriving during the save remain pending
            var targetGeneration = Volatile.Read(ref _requestedGeneration);
            if (targetGeneration == Volatile.Read(ref _completedGeneration)) return;

            // 保存が完了した場合だけ、固定した要求番号までを処理済みにする
            // Mark only the frozen generation complete after the save operation succeeds
            Save();
            Volatile.Write(ref _completedGeneration, targetGeneration);
            UnityEngine.Debug.Log("ワールドを保存しました");

            // 完了番号を通知し、終了待ち側が「どこまで書けたか」で判定できるようにする
            // Publish the completed generation so a shutdown waiter can judge how far the write got
            _onWorldSaveCompleted.OnNext(targetGeneration);
        }

        private void Save()
        {
            // 書き込み途中のクラッシュでセーブが破損しないようアトミックに書き込む
            // Write atomically so a mid-write crash cannot corrupt the save file
            var targetPath = _worldDataDirectory.SaveJsonFilePath;
            var tmpPath = targetPath + ".tmp";
            var backupPath = targetPath + ".bak";

            // まず一時ファイルへ全内容を書き切る
            // First write the full contents to a temporary file
            File.WriteAllText(tmpPath, _assembleSaveJsonText.AssembleSaveJson());

            // 既存ファイルがあれば直前バックアップ付きで置換、無ければ単純に移動
            // Replace existing file with a prior-version backup, or move directly on first save
            if (File.Exists(targetPath))
            {
                File.Replace(tmpPath, targetPath, backupPath);
            }
            else
            {
                File.Move(tmpPath, targetPath);
            }
        }
    }
}
