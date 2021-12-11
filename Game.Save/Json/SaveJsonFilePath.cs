namespace Game.Save.Json
{
    public class SaveJsonFilePath
    {
        public string FilePath => _filePath;
        private string _filePath;

        public SaveJsonFilePath(string filePath)
        {
            _filePath = filePath;
        }
    }
}