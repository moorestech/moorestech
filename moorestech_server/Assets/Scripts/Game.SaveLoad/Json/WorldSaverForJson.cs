using System.IO;
using Game.SaveLoad.Interface;

namespace Game.SaveLoad.Json
{
    public class WorldSaverForJson : IWorldSaveDataSaver
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly SaveJsonFilePath _filePath;
        
        public WorldSaverForJson(SaveJsonFilePath filePath, AssembleSaveJsonText assembleSaveJsonText)
        {
            _filePath = filePath;
            _assembleSaveJsonText = assembleSaveJsonText;
        }
        
        public void Save()
        {
            // 書き込み途中のクラッシュでセーブが破損しないようアトミックに書き込む
            // Write atomically so a mid-write crash cannot corrupt the save file
            var targetPath = _filePath.Path;
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