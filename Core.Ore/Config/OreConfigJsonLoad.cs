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
                var itemConfigData = JsonConvert.DeserializeObject<OreConfigJsonData[]>(oreConfig[modIds]);
                configList.AddRange(itemConfigData.Select(c => new OreConfigData(modIds, c)));
            }

            return configList;
        }
    }

    public class OreConfigData
    {
        public readonly string ModId;
        
        public readonly int OreId;
        public readonly string Name;
        public readonly int MiningItemId;
        public readonly float VeinSize;
        public readonly float VeinFrequency;
        public readonly int Priority;

        public OreConfigData(string modId,OreConfigJsonData oreConfigJsonData)
        {
            ModId = modId;
            OreId = oreConfigJsonData.OreId;
            Name = oreConfigJsonData.Name;
            MiningItemId = oreConfigJsonData.MiningItem;
            VeinSize = oreConfigJsonData.VeinSize;
            VeinFrequency = oreConfigJsonData.VeinFrequency;
            Priority = oreConfigJsonData.Priority;
        }
    }
}