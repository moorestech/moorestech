namespace Game.Save.Json
{
    public class SaveJsonFileName
    {
        public string FilePath => _filePath;
        private string _filePath;

        public SaveJsonFileName(string filePath)
        {
            _filePath = filePath;
        }
    }
}