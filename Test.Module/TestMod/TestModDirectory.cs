using System;
using System.IO;

namespace Test.Module.TestMod
{
    public class TestModDirectory
    {
        public static string FolderDirectory
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Test.Module", "TestMod");
            }
        }
        
        public static string ConfigOnlyDirectory => Path.Combine(FolderDirectory, "ConfigOnly");
    }
}