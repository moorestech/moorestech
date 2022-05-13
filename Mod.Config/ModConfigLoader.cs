using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Mod.Config.Interface;
using Newtonsoft.Json;

namespace Mod.Config
{
    public class ModConfigLoader : IModConfigLoader
    {
        private LoadConfigContainer _loadConfigContainer;
        public LoadConfigContainer LoadModConfig(string modDirectory)
        {
            if (_loadConfigContainer != null)return _loadConfigContainer;
            
            //get zip file list
            var zipFileList = Directory.GetFiles(modDirectory, "*.zip");
            
        }

    }
}