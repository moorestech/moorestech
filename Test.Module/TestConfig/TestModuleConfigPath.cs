using System;
using System.IO;

namespace Test.Module.TestConfig
{
    public class TestModuleConfigPath
    {
        public string GetPath(string fileName)
        {
            return Path.Combine(FolderPath, fileName);
        }
        public static string FolderPath
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Test.Module", "TestConfig", "Json");
            }
        }
    }
}