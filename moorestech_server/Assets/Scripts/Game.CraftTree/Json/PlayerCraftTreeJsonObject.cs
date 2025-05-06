using System;
using System.Collections.Generic;
using Game.CraftTree.Models;
using Newtonsoft.Json;

namespace Game.CraftTree.Json
{
    public class PlayerCraftTreeJsonObject
    {
        [JsonProperty("playerId")] public int PlayerId { get; set; }
        [JsonProperty("currentTargetNode")] public Guid CurrentTargetNode { get; set; }
        [JsonProperty("craftTrees")] public List<CraftTreeNodeJsonObject> CraftTrees { get; set; } = new();
        
        // デシリアライズ用のコンストラクタ
        public PlayerCraftTreeJsonObject() { }
        
        // PlayerCraftTreeInfoからの変換コンストラクタ
        public PlayerCraftTreeJsonObject(int playerId, PlayerCraftTreeInfo playerCraftTreeInfo)
        {
            PlayerId = playerId;
            CurrentTargetNode = playerCraftTreeInfo.CurrentTargetNode;
            
            // クラフトツリーノードを変換
            foreach (var tree in playerCraftTreeInfo.CraftTrees.Values)
            {
                CraftTrees.Add(new CraftTreeNodeJsonObject(tree));
            }
        }
        
        // PlayerCraftTreeInfoに変換するメソッド
        public PlayerCraftTreeInfo ToPlayerCraftTreeInfo()
        {
            var craftTreesList = new List<CraftTreeNode>();
            
            // ノードを再帰的に復元
            foreach (var treeJson in CraftTrees)
            {
                craftTreesList.Add(treeJson.ToCraftTreeNode());
            }
            
            return new PlayerCraftTreeInfo(CurrentTargetNode, craftTreesList);
        }
    }
}