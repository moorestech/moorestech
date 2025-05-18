using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.CraftTree.Models
{
    public class CraftTreeNode
    {
        public Guid NodeId { get; } = Guid.NewGuid();
        
        public CraftTreeNode Parent { get; }
        
        public IReadOnlyList<CraftTreeNode> Children => _children;
        private readonly List<CraftTreeNode> _children = new();
        
        public ItemId TargetItemId { get; }
        
        public bool IsCompleted => CurrentCount >= RequiredCount;
        
        public int RequiredCount { get; }
        public int CurrentCount { get; private set; } = 0;
        
        
        public CraftTreeNode(ItemId targetItemId, int requiredCount, CraftTreeNode parent)
        {
            TargetItemId = targetItemId;
            RequiredCount = requiredCount;
            Parent = parent;
        }
        
        public CraftTreeNode(CraftTreeNodeMessagePack messagePack, CraftTreeNode parent)
        {
            Parent = parent;
            NodeId = messagePack.NodeId;
            TargetItemId = (ItemId)messagePack.TargetItemId;
            RequiredCount = messagePack.RequiredCount;
            CurrentCount = messagePack.CurrentCount;
            
            foreach (var child in messagePack.Children)
            {
                var childNode = new CraftTreeNode(child, this);
                _children.Add(childNode);
            }
        }
        
        public void ReplaceChildren(List<CraftTreeNode> children)
        {
            _children.Clear();
            _children.AddRange(children);
        }
        public void SetCurrentItemCount(int currentItemCount)
        {
            CurrentCount = currentItemCount;
        }
    }
}