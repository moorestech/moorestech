using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Research;
using Mooresmaster.Model.GameActionModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block.Research
{
    public class ResearchTreeElement : MonoBehaviour, ITreeViewElement
    {
        public IObservable<ResearchNodeData> OnClickResearchButton => _onClickResearchButton;
        private readonly Subject<ResearchNodeData> _onClickResearchButton = new();
        
        public ResearchNodeData Node { get; private set; }
        
        public RectTransform RectTransform => rectTransform;
        
        [SerializeField] private Vector2 baseIconSize = new(30, 30);
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private RectTransform connectLinePrefab;
        [SerializeField] private GameObject completeOverlay;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        
        [SerializeField] private RectTransform consumeItemIcons;
        [SerializeField] private RectTransform unlockItemIcons;
        
        [SerializeField] private UGuiTooltipTarget researchButtonTooltipTarget;
        [SerializeField] private Button researchButton;
        
        private bool _isInitialized = false;
        
        // 生成された接続線のリスト
        private readonly List<RectTransform> _connectLines = new();
        
        private void Awake()
        {
            researchButton.onClick.AddListener(() =>
            {
                if (Node.State == ResearchNodeState.Researchable)
                {
                    _onClickResearchButton.OnNext(Node);
                }
            });
        }
        
        public void SetResearchNode(ResearchNodeData node)
        {
            Node = node;
            
            completeOverlay.SetActive(node.State == ResearchNodeState.Completed);
            if (researchButton != null)
            {
                researchButton.interactable = node.State == ResearchNodeState.Researchable;
            }
            
            var master = node.MasterElement;
            var view = master.GraphViewSettings;
            rectTransform.anchoredPosition = view.UIPosition;
            rectTransform.localScale = view.UIScale;

            title.text = master.ResearchNodeName;
            description.text = master.ResearchNodeDescription;
            
            SetButtonToolTipText();
            
            // アイテムアイコンの生成は初回のみ
            // Item icon generation is only for the first time
            if (!_isInitialized)
            {
                CreateUnlockItemIcons();
                CreateConsumeItemIcons();
                _isInitialized = true;
            }
            
            #region Internal
            
            void SetButtonToolTipText()
            {
                var text = node.State switch
                {
                    ResearchNodeState.UnresearchableAllReasons => "Not enough research items.\nPrerequisite research not completed.",
                    ResearchNodeState.UnresearchableNotEnoughItem => "Not enough research items.",
                    ResearchNodeState.UnresearchableNotEnoughPreNode => "Prerequisite research not completed.",
                    ResearchNodeState.Researchable => "Click to research",
                    ResearchNodeState.Completed => "Researched",
                    _ => ""
                };
                researchButtonTooltipTarget.SetText(text, false);
            }
            
            void CreateUnlockItemIcons()
            {
                var unlockItems = node.MasterElement.ClearedActions.items.Where(a => a.GameActionType == GameActionElement.GameActionTypeConst.unlockItemRecipeView);
                foreach (var unlockItem in unlockItems)
                {
                    var param = (UnlockItemRecipeViewGameActionParam)unlockItem.GameActionParam;
                    foreach (var unlockItemGuid in param.UnlockItemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(unlockItemGuid);
                        var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                        
                        var icon = Instantiate(ItemSlotView.Prefab, unlockItemIcons);
                        icon.SetItem(itemView, 0);
                        icon.SetSizeDelta(baseIconSize);
                    }
                }

                var giveItemActions = node.MasterElement.ClearedActions.items.Where(a => a.GameActionType == GameActionElement.GameActionTypeConst.giveItem);
                foreach (var giveItem in giveItemActions)
                {
                    var param = (GiveItemGameActionParam)giveItem.GameActionParam;
                    
                    foreach (var reward in param.RewardItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(reward.ItemGuid);
                        var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);

                        var icon = Instantiate(ItemSlotView.Prefab, unlockItemIcons);
                        icon.SetItem(itemView, reward.ItemCount);
                        icon.SetSizeDelta(baseIconSize);
                    }
                }
            }
            
            void CreateConsumeItemIcons()
            {
                foreach (var consumeItem in node.MasterElement.ConsumeItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(consumeItem.ItemGuid);
                    var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    var icon = Instantiate(ItemSlotView.Prefab, consumeItemIcons);
                    icon.SetItem(itemView, consumeItem.ItemCount);
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
        
        private void Update()
        {
            
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
