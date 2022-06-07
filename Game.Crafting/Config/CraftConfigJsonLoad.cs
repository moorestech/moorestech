using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Core.ConfigJson;
using Newtonsoft.Json;

namespace Game.Crafting.Config
{
    public class CraftConfigJsonLoad
    {
        public List<CraftConfigDataElement> Load(List<string> jsons)
        {
            return jsons.SelectMany(JsonConvert.DeserializeObject<CraftConfigDataElement[]>).ToList();
        }
    }
}