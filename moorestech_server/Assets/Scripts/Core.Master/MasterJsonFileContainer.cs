using System.Collections.Generic;
using System.Linq;

namespace Core.Master
{
    public class MasterJsonFileContainer
    {
        public readonly List<ModId> SortedModIds;
        
        public readonly List<MasterJsonContents> ConfigJsons;
        
        public MasterJsonFileContainer(List<MasterJsonContents> configs)
        {
            ConfigJsons = configs;
            SortedModIds = configs.Select(x => x.ModId).ToList();
        }
        
    }
}