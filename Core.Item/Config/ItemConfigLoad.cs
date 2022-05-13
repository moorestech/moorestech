using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using Core.Config.Item;
using Newtonsoft.Json;

namespace Core.Item.Config
{
    public class ItemConfigLoad
    {
        public List<ItemConfigData> LoadFromJsons(List<string> jsons)
        {
            return jsons.SelectMany(JsonConvert.DeserializeObject<ItemConfigData[]>).ToList();
        }
    }
}