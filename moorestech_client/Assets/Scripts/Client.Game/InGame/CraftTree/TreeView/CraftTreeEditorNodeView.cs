using System.Collections.Generic;
using Game.CraftTree;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorNodeView : MonoBehaviour
    {
        [SerializeField] private RectTransform uiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        public CraftTreeNode Node { get; private set; }
        private List<CraftTreeEditorNodeView> _children;
        
        public void Initialize(List<CraftTreeEditorNodeView> children, CraftTreeNode node, int depth)
        {
            var position = uiTransform.anchoredPosition;
            position.x = depth * depthWidth;
            uiTransform.anchoredPosition = position;
            
            Node = node;
            _children = children;
        }
    }
}