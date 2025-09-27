using System.Collections.Generic;
using System.IO;
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
                configs.Add(new MasterJsonContents(modId,masterJsonContents));
            }
            
            return configs;
        }
    }
}