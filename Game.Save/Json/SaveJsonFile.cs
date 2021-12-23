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

        public SaveJsonFile(SaveJsonFileName fileName,AssembleSaveJsonText assembleSaveJsonText)
        {
            _fileName = fileName;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        public void Save()
        {
            File.AppendAllText(_fileName.FullSaveFilePath, _assembleSaveJsonText.AssembleSaveJson());
        }
    }
}