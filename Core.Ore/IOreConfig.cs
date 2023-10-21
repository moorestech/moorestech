using System.Collections.Generic;
using Core.Ore.Config;

namespace Core.Ore
{
    /// <summary>
    ///     
    /// </summary>
    public interface IOreConfig
    {
        public int OreIdToItemId(int oreId);
        public List<int> GetSortedIdsForPriority();
        public OreConfigData Get(int oreId);
        public List<int> GetOreIds(string modId);
    }
}