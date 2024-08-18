using System.Collections.Generic;
using System.IO;
using Core.Master;
using Mod.Loader;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        //TODO こういうコンフィグ類を、パスを追加するだけでいい感じに管理できるようにしたい
        private const string ItemConfigPath = "config/item.json";
        private const string BlockConfigPath = "config/block.json";
        private const string MachineRecipeConfigPath = "config/machineRecipe.json";
        private const string CraftRecipeConfigPath = "config/craftRecipe.json";
        private const string MapObjectConfigPath = "config/mapObject.json";
        private const string ChallengeConfigPath = "config/challenge.json";
        
        public static Dictionary<string, ConfigJson> GetConfigString(ModsResource modResource)
        {
            // TODO 上が不要になったら下のコードを使うようにする
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
            
            //TODO 下のコードが不要になったらこのreturnを使う return configs;
            
            // -------------下は旧コード------------
            
            
            var configDict = new Dictionary<string, ConfigJson>();
            
            //zipファイルの中身のjsonファイルを読み込む
            foreach (var mod in modResource.Mods)
            {
                var modIdStr = mod.Value.ModMetaJson.ModId;
                var extractedPath = mod.Value.ExtractedPath;
                
                var itemConfigJson = LoadConfigFile(extractedPath, ItemConfigPath);
                var blockConfigJson = LoadConfigFile(extractedPath, BlockConfigPath);
                var machineRecipeConfigJson = LoadConfigFile(extractedPath, MachineRecipeConfigPath);
                var craftRecipeConfigJson = LoadConfigFile(extractedPath, CraftRecipeConfigPath);
                var mapObjectConfigJson = LoadConfigFile(extractedPath, MapObjectConfigPath);
                var challengeConfigJson = LoadConfigFile(extractedPath, ChallengeConfigPath);
                
                
                
                
                var modId = new ModId(mod.Value.ModMetaJson.ModId);
                
                // master/ 以下のjsonファイルをすべて取得する
                var masterJsonContents = new Dictionary<JsonFileName, string>();
                foreach (var masterJsonPath in Directory.GetFiles(extractedPath, "master/*.json"))
                {
                    var fileName = new JsonFileName(Path.GetFileNameWithoutExtension(masterJsonPath));
                    var jsonContents = File.ReadAllText(masterJsonPath);
                    masterJsonContents.Add(fileName, jsonContents);
                }
                configs.Add(new ConfigJson(modId,masterJsonContents));
                
                
                configDict.Add(modIdStr, new ConfigJson(itemConfigJson,
                    blockConfigJson,
                    machineRecipeConfigJson,
                    craftRecipeConfigJson,
                    mapObjectConfigJson,
                    challengeConfigJson,modId, masterJsonContents));
            }
            
            return configDict;
        }
        
        private static string LoadConfigFile(string extractedPath, string configPath)
        {
            var fullPath = Path.Combine(extractedPath, configPath);
            
            return !File.Exists(fullPath) ? string.Empty : File.ReadAllText(fullPath);
        }
    }
}