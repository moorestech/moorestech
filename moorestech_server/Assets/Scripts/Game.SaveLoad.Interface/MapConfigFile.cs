using System.IO;

namespace Game.SaveLoad.Interface
{
    public class MapConfigFile
    {
        public MapConfigFile(string fullMapObjectConfigFilePath)
        {
            FullMapObjectConfigFilePath = Path.Combine(fullMapObjectConfigFilePath, "mapObjects.json");
        }

        public string FullMapObjectConfigFilePath { get; }
    }
}