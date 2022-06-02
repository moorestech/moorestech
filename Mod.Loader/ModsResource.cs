using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Mod.Loader
{
    public class ModsResource : IDisposable
    {
        public readonly Dictionary<string, Mod> Mods = new Dictionary<string, Mod>();
        
        
        private const string ModMetaFilePath = "mod_meta.json";
        public ModsResource(string modDirectory)
        {
            
            foreach (var zipFile in ModFileList.Get(modDirectory))
            {
                var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
                var modMeta = JsonConvert.DeserializeObject<ModMetaJson>(LoadConfigFromZip(zip,ModMetaFilePath));
                
                if (modMeta == null)
                {
                    Console.WriteLine("Mod meta file not found in " + zipFile);
                    continue;
                }
                
                //extract zip
                
                
                Mods.Add(modMeta.ModId, new Mod(zip,modMeta,""));
            }
        }

        private string LoadConfigFromZip(ZipArchive zip, string configPath)
        {
            var config = zip.GetEntry(configPath);
            if (config == null) return string.Empty;
            
            using var itemJsonStream = config.Open();
            using var itemJsonString = new StreamReader(itemJsonStream);
            return itemJsonString.ReadToEnd();
        }

        public void Dispose()
        {
            foreach (var mod in Mods)
            {
                mod.Value.ZipArchive.Dispose();
            }
            Mods.Clear();
        }
    }

    public class Mod
    {
        public readonly ZipArchive ZipArchive;
        public readonly string ExtractedPath; 
            
        public readonly ModMetaJson ModMetaJson;

        public Mod(ZipArchive zipArchive, ModMetaJson modMetaJson, string extractedPath)
        {
            ZipArchive = zipArchive;
            ModMetaJson = modMetaJson;
            this.ExtractedPath = extractedPath;
        }
    }
}