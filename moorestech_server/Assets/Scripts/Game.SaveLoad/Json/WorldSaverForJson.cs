using System.IO;
using Game.Save.Interface;

namespace Game.Save.Json
{
    public class WorldSaverForJson : IWorldSaveDataSaver
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly SaveJsonFileName _fileName;

        public WorldSaverForJson(SaveJsonFileName fileName, AssembleSaveJsonText assembleSaveJsonText)
        {
            _fileName = fileName;
            _assembleSaveJsonText = assembleSaveJsonText;
        }

        public void Save()
        {
            File.WriteAllText(_fileName.FullSaveFilePath, _assembleSaveJsonText.AssembleSaveJson());
        }
    }
}