using System;
using System.IO;

namespace Tests.Module.TestMod
{
    public class TestModDirectory
    {
        private static string FolderDirectory => Path.Combine(Environment.CurrentDirectory, "../", "moorestech_server", "Assets", "Scripts", "", "Tests.Module", "TestMod");
        
        public static string ConfigOnlyDirectory => Path.Combine(FolderDirectory, "ConfigOnly");
        public static string ForUnitTestModDirectory => Path.Combine(FolderDirectory, "ForUnitTest");
        
        public static string MoorestechAlphaModDirectory => Path.Combine(Environment.CurrentDirectory, "../", "moorestech_client", "Server");
    }
}