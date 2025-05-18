using System;
using System.Collections.Generic;
using Core.Master;
using Game.CraftTree.Models;
using Newtonsoft.Json;

namespace Game.CraftTree.Json
{
    public class CraftTreeNodeJsonObject
    {
        [JsonProperty("nodeId")] public Guid NodeId { get; set; }
        [JsonProperty("targetItemId")] public int TargetItemId { get; set; }
        [JsonProperty("requiredCount")] public int RequiredCount { get; set; }
        [JsonProperty("currentCount")] public int CurrentCount { get; set; }
        [JsonProperty("children")] public List<CraftTreeNodeJsonObject> Children { get; set; } = new();
        
        // デシリアライズ用のコンストラクタ
        public CraftTreeNodeJsonObject() { }
        
        // CraftTreeNodeからの変換コンストラクタ
        public CraftTreeNodeJsonObject(CraftTreeNode node)
        {
            NodeId = node.NodeId;
            TargetItemId = (int)node.TargetItemId;
            RequiredCount = node.RequiredCount;
            CurrentCount = node.CurrentCount;
            
            // 子ノードを再帰的に変換
            foreach (var child in node.Children)
            {
                Children.Add(new CraftTreeNodeJsonObject(child));
            }
        }
        
        // CraftTreeNodeに変換するメソッド
        public CraftTreeNode ToCraftTreeNode(CraftTreeNode parent = null)
        {
            var node = new CraftTreeNode(new ItemId(TargetItemId), RequiredCount, parent);
            
            // 現在のカウントを設定
            node.SetCurrentItemCount(CurrentCount);
            
            // 子ノードを再帰的に復元
            var childNodes = new List<CraftTreeNode>();
            foreach (var childJson in Children)
            {
                childNodes.Add(childJson.ToCraftTreeNode(node));
            }
            
            node.ReplaceChildren(childNodes);
            return node;
        }
    }
}