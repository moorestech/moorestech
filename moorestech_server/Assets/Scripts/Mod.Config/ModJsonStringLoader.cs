using System.Collections.Generic;
using System.IO;
using Core.ConfigJson;
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
            var configDict = new Dictionary<string, ConfigJson>();
            
            //zipファイルの中身のjsonファイルを読み込む
            foreach (var mod in modResource.Mods)
            {
                var modId = mod.Value.ModMetaJson.ModId;
                var extractedPath = mod.Value.ExtractedPath;
                
                var itemConfigJson = LoadConfigFile(extractedPath, ItemConfigPath);
                var blockConfigJson = LoadConfigFile(extractedPath, BlockConfigPath);
                var machineRecipeConfigJson = LoadConfigFile(extractedPath, MachineRecipeConfigPath);
                var craftRecipeConfigJson = LoadConfigFile(extractedPath, CraftRecipeConfigPath);
                var mapObjectConfigJson = LoadConfigFile(extractedPath, MapObjectConfigPath);
                var challengeConfigJson = LoadConfigFile(extractedPath, ChallengeConfigPath);
                
                configDict.Add(modId, new ConfigJson(
                    modId,
                    itemConfigJson,
                    blockConfigJson,
                    machineRecipeConfigJson,
                    craftRecipeConfigJson,
                    mapObjectConfigJson,
                    challengeConfigJson));
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