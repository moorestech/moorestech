using System;
using System.IO;
using Game.Paths;

namespace Game.Save.Json
{
    /// <summary>
    /// JSONでセーブするさいのファイル名を指定するクラス
    /// </summary>
    public class SaveJsonFileName
    {
        public string FullSaveFilePath => _filePath;
        public string SaveFileDirectory => Path.GetDirectoryName(_filePath);
        
        
        private readonly string _filePath;

        public SaveJsonFileName(string fileName)
        {
            _filePath = SystemPath.GetSaveFilePath(fileName);
        }
    }
}