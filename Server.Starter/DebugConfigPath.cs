using System;
using System.IO;

namespace Server
{
    public class DebugConfigPath
    {
        public static string FolderPath
        {
            get
            {
                DirectoryInfo di = new DirectoryInfo(Environment.CurrentDirectory);
                DirectoryInfo diParent = di.Parent.Parent.Parent.Parent;
                return Path.Combine(diParent.FullName, "Server.Starter", "ReleaseConfig");
            }
        }
    }
}