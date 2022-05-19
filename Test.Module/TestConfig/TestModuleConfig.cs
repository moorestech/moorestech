using System;
using System.Collections.Generic;
using System.IO;

namespace Test.Module.TestConfig
{
    public class TestModuleConfig
    {
        private static string FolderDirectory
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Test.Module", "TestConfig","Json");
            }
        }
        
        public static Dictionary<string,string> AllMachineBlockConfigJson => new()
        {
            {"testMod",File.ReadAllText(Path.Combine(FolderDirectory, "All Machine Block Config.json"))}
        };
        public static Dictionary<string,string> UnitTestBlockConfigJson => new()
        {
            {"testMod",File.ReadAllText(Path.Combine(FolderDirectory, "Unit Test Block Config.json"))}
        };

        public static List<string> Mods = new() {"testMod"};
    }
}