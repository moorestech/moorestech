using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Challenge;
using Mooresmaster.Model.ResearchModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeView : MonoBehaviour
    {
        [SerializeField] private ResearchTreeElement nodeElementPrefab;
        
        [SerializeField] private Transform nodeListParent;
        [SerializeField] private Transform connectLineParent; // 線は一番下に表示される必要があるため専用の親に格納する

        [SerializeField] private RectTransform resizeTarget;
        [SerializeField] private RectTransform offsetTarget;

        private readonly Dictionary<Guid, ResearchTreeElement> _nodeElements = new();

        public void SetResearchNodes(IEnumerable<ResearchNodeData> nodes)
        {
            // 既存の要素をクリア
            ClearNodeElements();

            // 新しいノード要素を作成
            foreach (var node in nodes)
            {
                var nodeElement = Instantiate(nodeElementPrefab, nodeListParent);
                nodeElement.SetResearchNode(node);
                _nodeElements.Add(node.MasterElement.ResearchNodeGuid, nodeElement);
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

        private void ClearNodeElements()
        {
            foreach (var element in _nodeElements.Values)
            {
                if (element != null)
                {
                    Destroy(element.gameObject);
                }
            }
            _nodeElements.Clear();
        }
    }
}
