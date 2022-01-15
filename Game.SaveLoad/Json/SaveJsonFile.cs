using System;
using System.IO;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.World.Interface;

namespace Game.Save.Json
{
    public class SaveJsonFile : ISaveRepository
    {
        private readonly SaveJsonFileName _fileName;
        private readonly AssembleSaveJsonText _assembleSaveJsonText;

        public SaveJsonFile(SaveJsonFileName fileName, AssembleSaveJsonText assembleSaveJsonText)
        {
            _fileName = fileName;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        public void Save()
        {
            if (!Directory.Exists(_fileName.SaveFileDirectoryPath))
            {
                Directory.CreateDirectory(_fileName.SaveFileDirectoryPath);
            }

            File.WriteAllText(_fileName.FullSaveFilePath, _assembleSaveJsonText.AssembleSaveJson());
        }
    }
}