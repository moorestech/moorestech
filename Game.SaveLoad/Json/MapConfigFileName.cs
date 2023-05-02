namespace Game.Save.Json
{
    public class MapConfigFileName
    {
        public string FullMapObjectConfigFilePath { get; }
        
        public MapConfigFileName(string fullMapObjectConfigFilePath)
        {
            FullMapObjectConfigFilePath = fullMapObjectConfigFilePath;
        }
    }
}