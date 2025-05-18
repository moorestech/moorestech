using System.Collections.Generic;
using System.Linq;
using Game.CraftTree.Json;
using Game.CraftTree.Models;

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
        
        public List<PlayerCraftTreeJsonObject> GetSaveJsonObject()
        {
            var jsonObjects = new List<PlayerCraftTreeJsonObject>();
            
            foreach (var pair in _craftTree)
            {
                var playerId = pair.Key;
                var craftTreeInfo = pair.Value;
                
                if (craftTreeInfo != null)
                {
                    jsonObjects.Add(new PlayerCraftTreeJsonObject(playerId, craftTreeInfo));
                }
            }
            
            return jsonObjects;
        }
        
        public void LoadCraftTreeInfo(List<PlayerCraftTreeJsonObject> craftTreeInfoJsonObjects)
        {
            if (craftTreeInfoJsonObjects == null) return;
            
            _craftTree.Clear();
            foreach (var jsonObject in craftTreeInfoJsonObjects)
            {
                var playerId = jsonObject.PlayerId;
                var craftTreeInfo = jsonObject.ToPlayerCraftTreeInfo();
                _craftTree.Add(playerId, craftTreeInfo);
            }
        }
    }
}