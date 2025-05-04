using System.Collections.Generic;
using Game.CraftTree;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorView : MonoBehaviour
    {
        [SerializeField] private CraftTreeEditorNodeView nodePrefab;
        [SerializeField] private RectTransform content;
        
        public void Show(CraftTreeNode craftTreeNode)
        {
            CreateNode(craftTreeNode, 0);
            
            #region Internal
            
            CraftTreeEditorNodeView CreateNode(CraftTreeNode node, int depth)
            {
                var nodeView = Instantiate(nodePrefab, content);
                var children = new List<CraftTreeEditorNodeView>();
                
                foreach (var child in node.Children)
                {
                    children.Add(CreateNode(child, depth + 1));
                }
                
                nodeView.Initialize(children, node, depth);
                return nodeView;
            }
            
            #endregion
            
        }
    }
}