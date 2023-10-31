#if NET6_0
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
                var di = new DirectoryInfo(Environment.CurrentDirectory);
                var diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Test.Module", "TestMod");
            }
        }

        public static string ConfigOnlyDirectory => Path.Combine(FolderDirectory, "ConfigOnly");
        public static string ForUnitTestModDirectory => Path.Combine(FolderDirectory, "ForUnitTestMod");
        public static string MachineIoTestModDirectory => Path.Combine(FolderDirectory, "MachineIOTestMod");
    }
}
#endif