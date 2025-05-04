using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.CraftTree
{
    public class CraftTreeNode
    {
        public Guid NodeId { get; } = Guid.NewGuid();
        
        public IReadOnlyList<CraftTreeNode> Children => _children;
        private readonly List<CraftTreeNode> _children = new();
        
        public ItemId TargetItemId { get; }
        public int RequiredCount { get; }
        
        public int CurrentCount { get; } = 0;
        
        
        public CraftTreeNode(ItemId targetItemId, int requiredCount)
        {
            TargetItemId = targetItemId;
            RequiredCount = requiredCount;
        }
        
        public void AddChildren()
        {
            
        }
    }
}