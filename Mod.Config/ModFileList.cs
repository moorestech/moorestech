using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mod.Config
{
    public class ModFileList
    {
        public static List<string> Get(string modDirectory)
        {
            return Directory.GetFiles(modDirectory, "*.zip").ToList();
        }
    }
}