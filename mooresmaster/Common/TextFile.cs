namespace mooresmaster.Common
{
    public class TextFile
    {
        public readonly string FileName;
        public readonly string Content;

        public TextFile(string fileName, string content)
        {
            FileName = fileName;
            Content = content;
        }
    }
}