using System.Collections.Generic;

namespace Game.CraftTree
{
    public class CraftTreeManager
    {
        private readonly Dictionary<int,PlayerCraftTreeInfo> _craftTree = new();
        
        public void ApplyCraftTree(int playerId, PlayerCraftTreeInfo playerCraftTreeInfo)
        {
            _craftTree[playerId] = playerCraftTreeInfo;
        }
        
        public PlayerCraftTreeInfo GetCraftTreeInfo(int playerId)
        {
            return _craftTree.GetValueOrDefault(playerId);
        }
    }
}