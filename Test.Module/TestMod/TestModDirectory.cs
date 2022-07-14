using System;
using System.IO;

namespace Test.Module.TestMod
{
    public class TestModDirectory
    {
        private static string FolderDirectory
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Test.Module", "TestMod");
            }
        }
        
        public static string ConfigOnlyDirectory => Path.Combine(FolderDirectory, "ConfigOnly");
        public static string ForUnitTestModDirectory => Path.Combine(FolderDirectory, "ForUnitTestMod");
        public static string MachineIoTestModDirectory => Path.Combine(FolderDirectory, "MachineIOTestMod");
        public static string QuestTestModDirectory => Path.Combine(FolderDirectory, "QuestTestMod");
    }
}