using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Challenge;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeView : MonoBehaviour
    {
        public IObservable<ResearchNodeData> OnClickResearchButton => _onClickResearchButton;
        private readonly Subject<ResearchNodeData> _onClickResearchButton = new();
        
        [SerializeField] private ResearchTreeElement nodeElementPrefab;
        
        [SerializeField] private Transform nodeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する

        [SerializeField] private RectTransform resizeTarget;
        [SerializeField] private RectTransform offsetTarget;

        private Dictionary<Guid, ResearchTreeElement> _nodeElements;

        public void SetResearchNodes(List<ResearchNodeData> nodes)
        {
            if (_nodeElements == null)
            {
                CreateResearchTree();
                return;
            }
            
            UpdateResearchTree();
            
            #region Internal
            
            void CreateResearchTree()
            {
                _nodeElements = new Dictionary<Guid, ResearchTreeElement>();
                
                // 新しいノード要素を作成
                foreach (var node in nodes)
                {
                    var nodeElement = Instantiate(nodeElementPrefab, nodeListParent);
                    _nodeElements.Add(node.MasterElement.ResearchNodeGuid, nodeElement);
                    nodeElement.OnClickResearchButton.Subscribe(n => _onClickResearchButton.OnNext(n)).AddTo(this);
                    
                    nodeElement.SetResearchNode(node);
                }
                
                // 接続線を作成
                foreach (var nodeElement in _nodeElements.Values)
                {
                    nodeElement.CreateConnect(connectLineParent, _nodeElements);
                }
                
                // 全要素を包含するように親のサイズを調整
                var elements = _nodeElements.Values.Select(e => (ITreeViewElement)e);
                TreeViewAdjuster.AdjustParentSize(resizeTarget, offsetTarget, elements);
            }
            
            void UpdateResearchTree()
            {
                foreach (var node in nodes)
                {
                    if (_nodeElements.TryGetValue(node.MasterElement.ResearchNodeGuid, out var element))
                    {
                        element.SetResearchNode(node);
                    }
                }
            }
            
            #endregion
        }
    }
}
