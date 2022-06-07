using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Core.ConfigJson;
using Mod.Loader;
using Newtonsoft.Json;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        private const string ItemConfigPath = "config/item.json";
        private const string BlockConfigPath = "config/block.json";
        private const string MachineRecipeConfigPath = "config/machine_recipe.json";
        private const string CraftRecipeConfigPath = "config/craft_recipe.json";
        private const string OreConfigPath = "config/ore.json";
        
        
        public static Dictionary<string,ConfigJson> GetConfigString(string modDirectory)
        {
            
            using var modResource = new ModsResource(modDirectory);
            
            var configDict = new Dictionary<string, ConfigJson>();
            //zipファイルの中身のjsonファイルを読み込む
            foreach (var mod in modResource.Mods)
            {
                var zip = mod.Value.ZipArchive;
                
                var itemConfigJson = LoadConfigFromZip(zip,ItemConfigPath);
                var blockConfigJson = LoadConfigFromZip(zip,BlockConfigPath);
                var machineRecipeConfigJson = LoadConfigFromZip(zip,MachineRecipeConfigPath);
                var craftRecipeConfigJson = LoadConfigFromZip(zip,CraftRecipeConfigPath);
                var oreConfigJson = LoadConfigFromZip(zip,OreConfigPath);
                
                configDict.Add(mod.Value.ModMetaJson.ModId,new ConfigJson(mod.Value.ModMetaJson.ModId,itemConfigJson,blockConfigJson,machineRecipeConfigJson,craftRecipeConfigJson,oreConfigJson));
            }

            return configDict;
        }

        private static string LoadConfigFromZip(ZipArchive zip, string configPath)
        {
            var config = zip.GetEntry(configPath);
            if (config == null) return string.Empty;
            
            using var itemJsonStream = config.Open();
            using var itemJsonString = new StreamReader(itemJsonStream);
            return itemJsonString.ReadToEnd();
        }
    }
}