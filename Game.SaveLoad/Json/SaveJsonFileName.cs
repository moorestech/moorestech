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
        public string FullSaveFilePath { get; }

        public SaveJsonFileName(string fileName)
        {
            FullSaveFilePath = GameSystemPaths.GetSaveFilePath(fileName);
        }
    }
}