using System.Collections.Generic;
using System.IO;
using Core.Master;
using Mod.Loader;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        public static List<ConfigJson> GetConfigString(ModsResource modResource)
        {
            var configs = new List<ConfigJson>();
            
            //展開済みzipファイルの中身のjsonファイルを読み込む
            foreach (var mod in modResource.Mods)
            {
                var modId = new ModId(mod.Value.ModMetaJson.ModId);
                var extractedPath = mod.Value.ExtractedPath;
                
                // master/ 以下のjsonファイルをすべて取得する
                var masterJsonContents = new Dictionary<JsonFileName, string>();
                foreach (var masterJsonPath in Directory.GetFiles(extractedPath, "master/*.json"))
                {
                    var fileName = new JsonFileName(Path.GetFileNameWithoutExtension(masterJsonPath));
                    var jsonContents = File.ReadAllText(masterJsonPath);
                    masterJsonContents.Add(fileName, jsonContents);
                }
                configs.Add(new ConfigJson(modId,masterJsonContents));
            }
            
            return configs;
        }
        
        private static string LoadConfigFile(string extractedPath, string configPath)
        {
            var fullPath = Path.Combine(extractedPath, configPath);
            
            return !File.Exists(fullPath) ? string.Empty : File.ReadAllText(fullPath);
        }
    }
}