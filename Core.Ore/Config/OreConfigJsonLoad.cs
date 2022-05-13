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
        public List<OreConfigDataElement> Load(List<string> jsons)
        {
            return jsons.SelectMany(JsonConvert.DeserializeObject<OreConfigDataElement[]>).ToList();
        }
    }
}