using System;
using System.Collections.Generic;
using Core.Master;
using Game.CraftTree.Models;
using MessagePack;

namespace Game.CraftTree
{
    [MessagePackObject]
    public class CraftTreeNodeMessagePack
    {
        [Key(0)] public Guid NodeId { get; set; }
        [Key(1)] public List<CraftTreeNodeMessagePack> Children { get; set; }
        [Key(2)] public int TargetItemId { get; set; }
        
        [Key(3)] public int RequiredCount { get; set; }
        [Key(4)] public int CurrentCount { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public CraftTreeNodeMessagePack() { }
        
        public CraftTreeNodeMessagePack(CraftTreeNode craftTreeNode)
        {
            NodeId = craftTreeNode.NodeId;
            TargetItemId = (int)craftTreeNode.TargetItemId;
            RequiredCount = craftTreeNode.RequiredCount;
            CurrentCount = craftTreeNode.CurrentCount;
            
            Children = new List<CraftTreeNodeMessagePack>();
            foreach (var child in craftTreeNode.Children)
            {
                var childMessagePack = new CraftTreeNodeMessagePack(child);
                Children.Add(childMessagePack);
            }
        }
        
        public CraftTreeNode CreateCraftTreeNode()
        {
            return new CraftTreeNode(this, null);
        }
    }
}