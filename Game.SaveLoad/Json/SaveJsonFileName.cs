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
        private readonly string _filePath;
        public string FullSaveFilePath => _filePath; 

        public SaveJsonFileName(string fileName)
        {
            _filePath = SystemPath.GetSaveFilePath(fileName);
        }
    }
}