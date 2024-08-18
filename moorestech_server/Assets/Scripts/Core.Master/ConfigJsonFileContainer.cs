using System.Collections.Generic;
using System.Linq;

namespace Core.Master
{
    public class ConfigJsonFileContainer
    {
        public readonly List<ConfigJson> ConfigJsons;
        
        public ConfigJsonFileContainer(List<ConfigJson> configJsons)
        {
            ConfigJsons = configJsons;
        }
    }
}