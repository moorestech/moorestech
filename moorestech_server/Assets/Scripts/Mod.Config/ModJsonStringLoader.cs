using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Master;
using Mod.Loader;

namespace Mod.Config
{
    public class ModJsonStringLoader
    {
        public static List<MasterJsonContents> GetMasterString(ModsResource modResource)
        {
            // TODO 上が不要になったら下のコードを使うようにする
            var configs = new List<MasterJsonContents>();
            
            // Masterとローカライズが共有するmod上書き順をModIdで確定する
            // Fix the shared master/localization mod override order by ModId
            foreach (var mod in modResource.Mods.OrderBy(pair => pair.Key, StringComparer.Ordinal))
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
                configs.Add(new MasterJsonContents(modId,masterJsonContents));
            }
            
            return configs;
        }
    }
}
