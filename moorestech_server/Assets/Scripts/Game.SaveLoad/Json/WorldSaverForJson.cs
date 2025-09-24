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
            File.WriteAllText(_filePath.Path, _assembleSaveJsonText.AssembleSaveJson());
        }
    }
}