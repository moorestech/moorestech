using System.IO;

namespace Game.Save.Interface
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