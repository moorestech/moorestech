using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Core.ConfigJson;
using Core.Item.Config;
using Newtonsoft.Json;

namespace Core.Ore.Config
{
    public class OreConfigJsonLoad
    {
        public List<OreConfigData> Load(List<string> sortedModIds,Dictionary<string,string> oreConfig)
        {
            var configList = new List<OreConfigData>();
            foreach (var modIds in sortedModIds)
            {
                if (!oreConfig.TryGetValue(modIds, out var config))
                {
                    continue;
                }
                //TODO ログ基盤に入れる
                foreach (var oreConfigJsonData in JsonConvert.DeserializeObject<OreConfigJsonData[]>(config))
                {
                    var id = configList.Count;
                    configList.Add(new OreConfigData(modIds,id,oreConfigJsonData));
                }
            }

            return configList;
        }
    }

    public class OreConfigData
    {
        public readonly string ModId;
        
        public readonly int OreId;
        public readonly string Name;
        public readonly float VeinSize;
        public readonly float VeinFrequency;
        public readonly int Priority;
        
        public readonly string ItemName;
        public readonly string ItemModId;
        

        public OreConfigData(string modId,int oreId,OreConfigJsonData oreConfigJsonData)
        {
            ModId = modId;
            OreId = oreId;
            Name = oreConfigJsonData.Name;
            VeinSize = oreConfigJsonData.VeinSize;
            VeinFrequency = oreConfigJsonData.VeinFrequency;
            Priority = oreConfigJsonData.Priority;

            ItemName = oreConfigJsonData.ItemName;
            ItemModId = oreConfigJsonData.ItemModId;
        }
    }
}