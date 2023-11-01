using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Ore.Config
{
    public class OreConfigJsonLoad
    {
        public List<OreConfigData> Load(List<string> sortedModIds, Dictionary<string, string> oreConfig)
        {
            var configList = new List<OreConfigData>();
            foreach (var modIds in sortedModIds)
            {
                if (!oreConfig.TryGetValue(modIds, out var config)) continue;
                //TODO ログ基盤に入れる
                //ID 0は何もないことを表すため、鉱石は1から始まる
                var oreId = 1;
                foreach (var oreConfigJsonData in JsonConvert.DeserializeObject<OreConfigJsonData[]>(config))
                {
                    configList.Add(new OreConfigData(modIds, oreId, oreConfigJsonData));
                    oreId++;
                }
            }

            return configList;
        }
    }

    public class OreConfigData
    {
        public readonly string ItemModId;

        public readonly string ItemName;
        public readonly string ModId;
        public readonly string Name;

        public readonly int OreId;
        public readonly int Priority;
        public readonly float VeinFrequency;
        public readonly float VeinSize;


        public OreConfigData(string modId, int oreId, OreConfigJsonData oreConfigJsonData)
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