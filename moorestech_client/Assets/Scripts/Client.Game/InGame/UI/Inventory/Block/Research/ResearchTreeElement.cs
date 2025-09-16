using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Mooresmaster.Model.ChallengeActionModule;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeElement : MonoBehaviour, ITreeViewElement
    {
        public RectTransform RectTransform => rectTransform;
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private RectTransform connectLinePrefab;
        [SerializeField] private GameObject completeOverlay;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        
        [SerializeField] private RectTransform unlockItemIcons;

        public ResearchNodeData Node { get; private set; }

        // 生成された接続線のリスト
        private readonly List<RectTransform> _connectLines = new();

        public void SetResearchNode(ResearchNodeData node)
        {
            Node = node;
            
            completeOverlay.SetActive(node.IsCompleted);
            
            var master = node.MasterElement;
            var view = master.GraphViewSettings;
            rectTransform.anchoredPosition = view.UIPosition;
            rectTransform.localScale = view.UIScale;

            title.text = master.ResearchNodetName;
            description.text = master.ResearchNodeDescription;
            
            CreateUnlockItemIcons();
            
            #region Internal
            
            void CreateUnlockItemIcons()
            {
                var unlockItems = node.MasterElement.ClearedActions.items.Where(a => a.ChallengeActionType == ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView);

                foreach (var unlockItem in unlockItems)
                {
                    var param = (UnlockItemRecipeViewChallengeActionParam)unlockItem.ChallengeActionParam;
                    foreach (var unlockItemGuid in param.UnlockItemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(unlockItemGuid);
                        var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                        
                        var icon = Instantiate(ItemSlotView.Prefab, unlockItemIcons);
                        icon.SetItem(itemView, 0);
                        icon.SetSizeDelta(new Vector2(30, 30));
                    }
                }
            }
            
            #endregion
        }

        public void CreateConnect(Transform lineParent, Dictionary<Guid, ResearchTreeElement> nodeElements)
        {
            // 既存の接続線をクリア
            ClearConnectLines();

            // 前のノードがある場合、線を引く
            var prevGuids = Node.MasterElement.PrevResearchNodeGuids;
            if (prevGuids == null || prevGuids.Length == 0) return;

            foreach (var prevGuid in prevGuids)
            {
                if (nodeElements.TryGetValue(prevGuid, out var prevNodeElement))
                {
                    CreateLine(prevNodeElement, lineParent);
                }
            }

            #region Internal

            void CreateLine(ResearchTreeElement prevElement, Transform parent)
            {
                var currentPosition = RectTransform.anchoredPosition;
                var targetPosition = prevElement.RectTransform.anchoredPosition;

                // 接続線を作成
                var connectLine = Instantiate(connectLinePrefab, transform);
                connectLine.gameObject.SetActive(true);

                var distance = Vector2.Distance(currentPosition, targetPosition);
                connectLine.sizeDelta = new Vector2(distance, connectLine.sizeDelta.y);

                var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
                connectLine.localEulerAngles = new Vector3(0, 0, angle);

                // 親の位置を変更
                connectLine.SetParent(parent);

                // 親によってスケールが変わっている可能性があるので戻す
                connectLine.localScale = Vector3.one;

                _connectLines.Add(connectLine);
            }

            #endregion
        }

        private void OnDestroy()
        {
            ClearConnectLines();
        }

        private void ClearConnectLines()
        {
            foreach (var line in _connectLines)
            {
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }
            _connectLines.Clear();
        }
    }
}
