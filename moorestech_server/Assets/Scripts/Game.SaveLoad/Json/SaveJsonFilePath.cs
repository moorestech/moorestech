using Game.Paths;

namespace Game.SaveLoad.Json
{
    /// <summary>
    ///     JSONでセーブするさいのファイル名を指定するクラス
    /// </summary>
    public class SaveJsonFilePath
    {
        public string Path { get; }
        
        public SaveJsonFilePath(string path)
        {
            Path = path;
        }
    }
}