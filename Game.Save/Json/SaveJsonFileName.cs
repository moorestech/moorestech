using System;
using System.IO;

namespace Game.Save.Json
{
    /// <summary>
    /// JSONでセーブするさいのファイル名を指定するクラス
    /// </summary>
    public class SaveJsonFileName
    {
        public string FullSaveFilePath => _filePath;
        public string SaveFileDirectoryPath => Path.GetDirectoryName(_filePath);
        private string _filePath;

        public SaveJsonFileName(string fileName)
        {
            ChangeFileName(fileName);
        }

        public void ChangeFileName(string fileName)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    _filePath = Path.Combine("C:", "Users", Environment.UserName, "AppData","Roaming",".moorestech","saves",fileName);
                    break;
                case PlatformID.Unix:
                    _filePath = Path.Combine("/Users", Environment.UserName, "Library","Application Support","moorestech","saves",fileName);
                    break;
            }
        }
    }
}