using System;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.World.Interface;

namespace Game.Save.Json
{
    public class SaveJsonFile : ISaveRepository
    {
        private readonly SaveJsonFilePath _filePath;
        private readonly AssembleSaveJsonText _assembleSaveJsonText;

        public SaveJsonFile(SaveJsonFilePath filePath,AssembleSaveJsonText assembleSaveJsonText)
        {
            _filePath = filePath;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        public void Save()
        {
        }
    }
}