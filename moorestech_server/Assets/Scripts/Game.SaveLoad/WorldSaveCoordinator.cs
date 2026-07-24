using System.IO;
using System.Threading;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;

namespace Game.SaveLoad
{
    public sealed class WorldSaveCoordinator : IWorldSaveRequest
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly WorldDataDirectory _worldDataDirectory;
        private long _requestedGeneration;
        private long _completedGeneration;

        public WorldSaveCoordinator(WorldDataDirectory worldDataDirectory, AssembleSaveJsonText assembleSaveJsonText)
        {
            _worldDataDirectory = worldDataDirectory;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        public void RequestSave()
        {
            Interlocked.Increment(ref _requestedGeneration);
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
