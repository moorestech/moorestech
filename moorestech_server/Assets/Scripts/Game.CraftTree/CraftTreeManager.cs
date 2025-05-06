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
        
        public Dictionary<int, PlayerCraftTreeInfo> GetAllCraftTreeInfo()
        {
            return new Dictionary<int, PlayerCraftTreeInfo>(_craftTree);
        }
        
        public void LoadCraftTreeInfo(Dictionary<int, PlayerCraftTreeInfo> craftTreeInfo)
        {
            if (craftTreeInfo == null) return;
            
            _craftTree.Clear();
            foreach (var pair in craftTreeInfo)
            {
                _craftTree.Add(pair.Key, pair.Value);
            }
        }
    }
}