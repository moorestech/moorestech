using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Game.Paths;
using Mod.Base;
using Newtonsoft.Json;

namespace Mod.Loader
{
    public class ModsResource
    {
        private const string ModMetaFilePath = "modMeta.json";
        public readonly Dictionary<string, Mod> Mods;


        ///     Mod

        public ModsResource(string modDirectory)
        {
            Mods = LoadModFromZip(modDirectory);
            foreach (var mod in LoadModFromFolder(modDirectory)) Mods.Add(mod.Key, mod.Value);
        }


        ///     Zipmod
        ///     Zip

        private static Dictionary<string, Mod> LoadModFromZip(string modDirectory)
        {
            var loadedMods = new Dictionary<string, Mod>();
            // zipmod
            foreach (var zipFile in Directory.GetFiles(modDirectory, "*.zip").ToList())
            {
                var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
                var modMeta = JsonConvert.DeserializeObject<ModMetaJson>(LoadConfigFromZip(zip, ModMetaFilePath));

                if (modMeta == null)
                {
                    Console.WriteLine("Mod meta file not found in " + zipFile);
                    continue;
                }

                //extract zip
                var extractedDir = ExtractModZip(zipFile, modMeta);
                zip.Dispose();

                loadedMods.Add(modMeta.ModId, new Mod(modMeta, extractedDir));
            }

            return loadedMods;
        }


        ///     mod

        private static Dictionary<string, Mod> LoadModFromFolder(string modDirectory)
        {
            var loadedMods = new Dictionary<string, Mod>();
            // mod
            foreach (var modDir in Directory.GetDirectories(modDirectory))
            {
                //mod meta
                var modMetaFile = Path.Combine(modDir, ModMetaFilePath);
                if (!File.Exists(modMetaFile))
                {
                    //TODO 
                    Console.WriteLine("Mod meta file not found in " + modDir);
                    continue;
                }

                var modMeta = JsonConvert.DeserializeObject<ModMetaJson>(File.ReadAllText(modMetaFile));

                loadedMods.Add(modMeta.ModId, new Mod(modMeta, modDir));
            }

            return loadedMods;
        }


        ///     Zip

        /// <param name="zip">Zip</param>
        /// <param name="configPath">Zip</param>
        /// <returns>JSON</returns>
        private static string LoadConfigFromZip(ZipArchive zip, string configPath)
        {
            var config = zip.GetEntry(configPath);
            if (config == null) return string.Empty;

            using var itemJsonStream = config.Open();
            using var itemJsonString = new StreamReader(itemJsonStream);
            return itemJsonString.ReadToEnd();
        }


        ///     Zip

        /// <returns>mod</returns>
        private static string ExtractModZip(string zipPath, ModMetaJson modMetaJson)
        {
            var fixModId = modMetaJson.ModId.ReplaceFileNotAvailableCharacter("-");
            var fixModVersion = modMetaJson.ModVersion.ReplaceFileNotAvailableCharacter("-");
            var sha1Hash = CalcFileHash.GetSha1Hash(zipPath);

            var folderName = $"{fixModId}_ver_{fixModVersion}_sha1_{sha1Hash}";

            var path = GameSystemPaths.GetExtractedModDirectory(folderName);
            
            if (Directory.EnumerateFileSystemEntries(path).Any())
                
                return path;
            
            ZipFile.ExtractToDirectory(zipPath, path);
            return path;
        }
    }

    public class Mod
    {
        public readonly string ExtractedPath;

        public readonly List<MoorestechServerModEntryPoint> ModEntryPoints;

        public readonly ModMetaJson ModMetaJson;

        public Mod(ModMetaJson modMetaJson, string extractedPath)
        {
            ModMetaJson = modMetaJson;
            ExtractedPath = extractedPath;
            ModEntryPoints = LoadEntryPoints(extractedPath);
        }

        private static List<MoorestechServerModEntryPoint> LoadEntryPoints(string modDirectory)
        {
            var entryPoints = new List<MoorestechServerModEntryPoint>();

            try
            {
                //moddllModBase
                foreach (var dllPath in Directory.GetFiles(modDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    var modBaseTypes = assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(MoorestechServerModEntryPoint)));
                    foreach (var modBaseType in modBaseTypes)
                    {
                        var modBase = (MoorestechServerModEntryPoint)Activator.CreateInstance(modBaseType);
                        entryPoints.Add(modBase);
                    }
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                
            }

            return entryPoints;
        }
    }
}