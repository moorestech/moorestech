using Game.Paths;

namespace Game.SaveLoad.Json
{
    /// <summary>
    ///     JSONでセーブするさいのファイル名を指定するクラス
    /// </summary>
    public class SaveJsonFileName
    {
        public SaveJsonFileName(string fileName)
        {
            FullSaveFilePath = GameSystemPaths.GetSaveFilePath(fileName);
        }

        public string FullSaveFilePath { get; }
    }
}