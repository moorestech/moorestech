using System.Collections.Generic;
using Game.CraftTree;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorView : MonoBehaviour
    {
        [SerializeField] private CraftTreeEditorNodeView nodePrefab;
        [SerializeField] private RectTransform content;
        
        private readonly List<CraftTreeEditorNodeView> _nodes = new();
        
        public void Show(CraftTreeNode craftTreeNode)
        {
            DestroyNodes();
            
            CreateNode(craftTreeNode, 0);
            
            #region Internal
            
            void DestroyNodes()
            {
                foreach (var node in _nodes)
                {
                    Destroy(node.gameObject);
                }
                _nodes.Clear();
            }
            
            CraftTreeEditorNodeView CreateNode(CraftTreeNode node, int depth)
            {
                var nodeView = Instantiate(nodePrefab, content);
                nodeView.OnUpdateNode.Subscribe(_ =>
                {
                    Show(craftTreeNode);
                });
                
                var children = new List<CraftTreeEditorNodeView>();
                
                foreach (var child in node.Children)
                {
                    children.Add(CreateNode(child, depth + 1));
                }
                
                nodeView.Initialize(children, node, depth);
                _nodes.Add(nodeView);
                return nodeView;
            }
            
            #endregion
            
        }
    }
}