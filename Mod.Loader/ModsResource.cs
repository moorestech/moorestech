using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Game.Paths;
using Newtonsoft.Json;

namespace Mod.Loader
{
    public class ModsResource 
    {
        public readonly Dictionary<string, Mod> Mods;
        
        
        private const string ModMetaFilePath = "modMeta.json";
        
        /// <summary>
        /// Mod内に格納されているリソースをロードする
        /// </summary>
        public ModsResource(string modDirectory)
        {
            Mods = LoadModFromZip(modDirectory);
            foreach (var mod in LoadModFromFolder(modDirectory))
            {
                Mods.Add(mod.Key,mod.Value);
            }
        }
        
        /// <summary>
        /// Zipファイル形式のmodをロードする
        /// Zipを展開して展開済みフォルダに入れ、そこからデータをロードする
        /// </summary>
        private static Dictionary<string, Mod> LoadModFromZip(string modDirectory)
        {
            var loadedMods = new Dictionary<string, Mod>();
            // zipファイルのmodをロードする
            foreach (var zipFile in Directory.GetFiles(modDirectory, "*.zip").ToList())
            {
                var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
                var modMeta = JsonConvert.DeserializeObject<ModMetaJson>(LoadConfigFromZip(zip,ModMetaFilePath));
                
                if (modMeta == null)
                {
                    Console.WriteLine("Mod meta file not found in " + zipFile);
                    continue;
                }
                
                //extract zip
                var extractedDir = ExtractModZip(zipFile, modMeta);
                zip.Dispose();
                
                loadedMods.Add(modMeta.ModId, new Mod(modMeta,extractedDir));
            }
            return loadedMods;
        }

        /// <summary>
        /// フォルダーのmodをロードする
        /// </summary>
        private static Dictionary<string, Mod> LoadModFromFolder(string modDirectory)
        {
            var loadedMods = new Dictionary<string, Mod>();
            // 通常のディレクトリのmodをロードする
            foreach (var modDir in Directory.GetDirectories(modDirectory))
            {
                //mod metaファイルがあるかどうかをチェック
                var modMetaFile = Path.Combine(modDir, ModMetaFilePath);
                if (!File.Exists(modMetaFile))
                {
                    //TODO ログ基盤に入れる
                    Console.WriteLine("Mod meta file not found in " + modDir);
                    continue;
                }
                var modMeta = JsonConvert.DeserializeObject<ModMetaJson>(File.ReadAllText(modMetaFile));

                loadedMods.Add(modMeta.ModId, new Mod(modMeta,modDir));
            }

            return loadedMods;
        }

        /// <summary>
        /// Zipを展開せず中身のコンフィグファイルを読み込む
        /// </summary>
        /// <param name="zip">Zipファイル</param>
        /// <param name="configPath">Zipファイル内のコンフィグのパス</param>
        /// <returns>ロードされたJSONのデータ</returns>
        private static string LoadConfigFromZip(ZipArchive zip, string configPath)
        {
            var config = zip.GetEntry(configPath);
            if (config == null) return string.Empty;
            
            using var itemJsonStream = config.Open();
            using var itemJsonString = new StreamReader(itemJsonStream);
            return itemJsonString.ReadToEnd();
        }

        /// <summary>
        /// Zipフォルダを展開し、指定されたフォルダにデータを移す
        /// </summary>
        /// <returns>展開後のmodのパス</returns>
        private static string ExtractModZip(string zipPath,ModMetaJson modMetaJson)
        {
            var fixModId = modMetaJson.ModId.ReplaceFileNotAvailableCharacter("-");
            var fixModVersion = modMetaJson.ModVersion.ReplaceFileNotAvailableCharacter("-");
            var sha1Hash = CalcFileHash.GetSha1Hash(zipPath);
            
            var folderName = $"{fixModId}_ver_{fixModVersion}_sha1_{sha1Hash}";

            var path = SystemPath.GetExtractedModDirectory(folderName);
            //ディレクトリの中身をチェック
            if (Directory.EnumerateFileSystemEntries(path).Any())
            {
                //すでに解凍済み
                return path;
            }
            //解凍を実行
            ZipFile.ExtractToDirectory(zipPath,path);
            return path;
        }
    }

    public class Mod
    {
        public readonly string ExtractedPath; 
            
        public readonly ModMetaJson ModMetaJson;

        public Mod(ModMetaJson modMetaJson, string extractedPath)
        {
            ModMetaJson = modMetaJson;
            this.ExtractedPath = extractedPath;
        }
    }
}