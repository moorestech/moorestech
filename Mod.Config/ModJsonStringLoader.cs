using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        private const string ItemConfigPath = "config/item.json";
        private const string BlockConfigPath = "config/block.json";
        private const string MachineRecipeConfigPath = "config/machine_recipe.json";
        private const string CraftRecipeConfigPath = "config/craft_recipe.json";
        
        
        public static Dictionary<string,ConfigJson> GetConfigString(string[] zipFileList)
        {
            var configDict = new Dictionary<string, ConfigJson>();
            //zipファイルの中身のjsonファイルを読み込む
            foreach (var zipFile in zipFileList)
            {
                using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);

                var itemConfigJson = LoadConfigFromZip(zip,ItemConfigPath);
                var blockConfigJson = LoadConfigFromZip(zip,BlockConfigPath);
                var machineRecipeConfigJson = LoadConfigFromZip(zip,MachineRecipeConfigPath);
                var craftRecipeConfigJson = LoadConfigFromZip(zip,CraftRecipeConfigPath);
                
                var zipFileName = Path.GetFileNameWithoutExtension(zipFile);
                
                configDict.Add(zipFileName,new ConfigJson(itemConfigJson,blockConfigJson,machineRecipeConfigJson,craftRecipeConfigJson));
            }

            return configDict;
        }

        private static string LoadConfigFromZip(ZipArchive zip, string configPath)
        {
            var itemJson = zip.GetEntry(configPath);
            if (itemJson == null) return string.Empty;
            
            using var itemJsonStream = itemJson.Open();
            using var itemJsonString = new StreamReader(itemJsonStream);
            return itemJsonString.ReadToEnd();
        }
    }

    public class ConfigJson
    {
        public readonly string ItemConfigJson;
        public readonly string BlockConfigJson;
        public readonly string MachineRecipeConfigJson;
        public readonly string CraftRecipeConfigJson;

        public ConfigJson(string itemJson, string blockConfigJson, string machineRecipeConfigJson, string craftRecipeConfigJson)
        {
            ItemConfigJson = itemJson;
            BlockConfigJson = blockConfigJson;
            MachineRecipeConfigJson = machineRecipeConfigJson;
            CraftRecipeConfigJson = craftRecipeConfigJson;
        }
    }
}