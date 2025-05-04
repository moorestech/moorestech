using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Game.CraftTree;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorView : MonoBehaviour
    {
        [SerializeField] private CraftTreeEditorNodeView nodePrefab;
        [SerializeField] private RectTransform content;
        [SerializeField] private VerticalLayoutGroup layoutGroup;
        
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private readonly List<CraftTreeEditorNodeView> _nodes = new();
        
        public void Initialize(ItemRecipeViewerDataContainer itemRecipe)
        {
            _itemRecipeViewerDataContainer = itemRecipe;
        }
        
        public void Show(CraftTreeNode craftTreeNode)
        {
            DestroyNodes();
            
            CreateNode(craftTreeNode, 0);
            
            // reference : https://medium.com/@sakastudio100/the-problem-of-a-missingreferenceexception-occurring-when-a-child-of-verticallayoutgroup-is-deleted-c2153b8ae311
            layoutGroup.CalculateLayoutInputHorizontal();
            layoutGroup.CalculateLayoutInputVertical();
            
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
                
                nodeView.Initialize(children, node, depth, _itemRecipeViewerDataContainer);
                _nodes.Add(nodeView);
                return nodeView;
            }
            
            #endregion
            
        }
    }
}