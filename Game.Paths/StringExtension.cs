namespace Game.Paths
{
    public static class StringExtension
    {
        public static string ReplaceFileNotAvailableCharacter(this string fileName, string replace)
        {
            fileName = fileName.Replace("/",replace);
            fileName = fileName.Replace("\\",replace);
            fileName = fileName.Replace(" ",replace);
            fileName = fileName.Replace(":",replace);
            fileName = fileName.Replace("*",replace);
            fileName = fileName.Replace("?",replace);
            fileName = fileName.Replace("\"",replace);
            fileName = fileName.Replace("<",replace);
            fileName = fileName.Replace(">",replace);
            fileName = fileName.Replace("|",replace);
            fileName = fileName.Replace(".",replace);
            return fileName;
        }
    }
}