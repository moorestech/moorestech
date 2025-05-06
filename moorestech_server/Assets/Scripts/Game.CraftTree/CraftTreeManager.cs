using System.Collections.Generic;
using System.Linq;

namespace Game.CraftTree
{
    public class CraftTreeManager
    {
        private readonly Dictionary<int,PlayerCraftTreeInfo> _craftTree = new();
        
        public void ApplyCraftTree(int playerId, PlayerCraftTreeInfo playerCraftTreeInfo)
        {
            _craftTree[playerId] = playerCraftTreeInfo;
        }
        
    }
}