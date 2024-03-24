using System.Collections.Generic;
using System.IO;
using Core.ConfigJson;
using Mod.Loader;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        private const string ItemConfigPath = "config/item.json";
        private const string BlockConfigPath = "config/block.json";
        private const string MachineRecipeConfigPath = "config/machineRecipe.json";
        private const string CraftRecipeConfigPath = "config/craftRecipe.json";
        private const string OreConfigPath = "config/ore.json";
        private const string QuestConfigPath = "config/quest.json";


        public static (Dictionary<string, ConfigJson> configJsons, ModsResource modsResource) GetConfigString(
            string modDirectory)
        {
            var modResource = new ModsResource(modDirectory);

            var configDict = new Dictionary<string, ConfigJson>();
            //zipファイルの中身のjsonファイルを読み込む
            foreach (KeyValuePair<string, Loader.Mod> mod in modResource.Mods)
            {
                var extractedPath = mod.Value.ExtractedPath;

                var itemConfigJson = LoadConfigFile(extractedPath, ItemConfigPath);
                var blockConfigJson = LoadConfigFile(extractedPath, BlockConfigPath);
                var machineRecipeConfigJson = LoadConfigFile(extractedPath, MachineRecipeConfigPath);
                var craftRecipeConfigJson = LoadConfigFile(extractedPath, CraftRecipeConfigPath);
                var oreConfigJson = LoadConfigFile(extractedPath, OreConfigPath);
                var questConfigJson = LoadConfigFile(extractedPath, QuestConfigPath);

                configDict.Add(mod.Value.ModMetaJson.ModId,
                    new ConfigJson(mod.Value.ModMetaJson.ModId, itemConfigJson, blockConfigJson,
                        machineRecipeConfigJson, craftRecipeConfigJson, oreConfigJson, questConfigJson));
            }

            return (configDict, modResource);
        }

        private static string LoadConfigFile(string extractedPath, string configPath)
        {
            var fullPath = Path.Combine(extractedPath, configPath);

            return !File.Exists(fullPath) ? string.Empty : File.ReadAllText(fullPath);
        }
    }
}