using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListUIElement : MonoBehaviour
    {
        public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
        public ChallengeMasterElement ChallengeMasterElement { get; private set; }
        public ChallengeListUIElementState CurrentState { get; private set; }
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private ItemSlotView itemSlotView;
        [SerializeField] private RectTransform connectLinePrefab;
        [SerializeField] private GameObject currentObject;
        [SerializeField] private GameObject completedObject;
        [SerializeField] private GameObject lockedObject;
        
        [SerializeField] private UGuiTooltipTarget uGuiTooltipTarget;
        
        // 生成された接続線のリスト
        // List of generated connection lines
        private readonly List<RectTransform> _connectLines = new();
        
        
        public void Initialize(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            SetUI();
            
            SetUIEnterTooltip();
            
            #region Internal
            
            void SetUI()
            {
                var param = challengeMasterElement.DisplayListParam;
                // 位置の指定
                // Position specification
                rectTransform.anchoredPosition = param.UIPosition;
                rectTransform.localScale = param.UIScale;
                
                // アイコンの指定
                // Icon specification
                if (param.IconItem != null)
                {
                    var itemView = ClientContext.ItemImageContainer.GetItemView(param.IconItem);
                    itemSlotView.SetItem(itemView, 0);
                }
                itemSlotView.SetSlotViewOption(new CommonSlotViewOption
                {
                    IsShowToolTip = false,
                });
            }
            
            void SetUIEnterTooltip()
            {
                var clearedActionsTest = string.Empty;
                foreach (var clearedActionsElement in challengeMasterElement.ClearedActions.items)
                {
                    switch (clearedActionsElement.GameActionType)
                    {
                        case GameActionElement.GameActionTypeConst.unlockCraftRecipe :
                            var recipeGuids = ((UnlockCraftRecipeGameActionParam)clearedActionsElement.GameActionParam).UnlockRecipeGuids;
                            var unlockRecipeText = "レシピ解放: \n\t";
                            foreach (var guid in recipeGuids)
                            {
                                var recipe = MasterHolder.CraftRecipeMaster.GetCraftRecipe(guid);
                                foreach (var requiredItem in recipe.RequiredItems)
                                {
                                    var requiredItemMaster = MasterHolder.ItemMaster.GetItemMaster(requiredItem.ItemGuid);
                                    unlockRecipeText += $"{requiredItemMaster.Name}x{requiredItem.Count} ";
                                }
                                var resultItemMaster = MasterHolder.ItemMaster.GetItemMaster(recipe.CraftResultItemGuid);
                                unlockRecipeText += $"\n\t=> {resultItemMaster.Name}x{recipe.CraftResultCount}";
                                
                                clearedActionsTest += unlockRecipeText;
                            }
                            break;
                        case GameActionElement.GameActionTypeConst.unlockItemRecipeView :
                            var itemGuids = ((UnlockItemRecipeViewGameActionParam)clearedActionsElement.GameActionParam).UnlockItemGuids;
                            clearedActionsTest += $"アイテム解放: ";
                            foreach (var guid in itemGuids)
                            {
                                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(guid);
                                clearedActionsTest += $"\n\t{itemMaster.Name}";
                            }
                            break;
                        case GameActionElement.GameActionTypeConst.giveItem :
                            var giveItemParam = (GiveItemGameActionParam)clearedActionsElement.GameActionParam;
                            var targetLabel = giveItemParam.DeliveryTarget == GiveItemGameActionParam.DeliveryTargetConst.allPlayers
                                ? "全員"
                                : "完了者のみ";
                            clearedActionsTest += $"アイテム支給({targetLabel}): ";
                            var rewardItems = giveItemParam.RewardItems;
                            foreach (var reward in rewardItems)
                            {
                                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(reward.ItemGuid);
                                clearedActionsTest += $"\n\t{itemMaster.Name} x{reward.ItemCount}";
                            }
                            break;
                    }
                }
                
                
                var text = @$"<size=30>{challengeMasterElement.Title}</size>
<size=20>    {challengeMasterElement.Summary}
達成報酬
    {clearedActionsTest}</size>";
                uGuiTooltipTarget.SetText(text, false);
            }
            
  #endregion
        }
        
        public void CreateConnect(Transform lineParent, Dictionary<Guid, ChallengeListUIElement> challengeListUIElements)
        {
            // 既存の接続線をクリア
            // Clear existing connection lines
            ClearConnectLines();
            
            // 前のチャレンジがある場合、線を引く
            // If there is a previous challenge, draw a line
            var prevGuids = ChallengeMasterElement.PrevChallengeGuids;
            if (prevGuids == null) return;
            
            foreach (var prev in prevGuids)
            {
                if (!challengeListUIElements.TryGetValue(prev, out var prevChallengeListUIElement)) continue;
                
                // 線を引く
                // Draw a line
                CreateLine(prevChallengeListUIElement, lineParent);
            }
            
            #region Internal
            
            void CreateLine(ChallengeListUIElement prevChallengeListUI, Transform parent)
            {
                // 線の長さと角度を計算して適用
                // Calculate and apply the length and angle of the line
                var currentPosition = AnchoredPosition;
                var targetPosition = prevChallengeListUI.AnchoredPosition;
                
                // 接続線を取得または作成
                // Get or create connection line
                var connectLine = Instantiate(connectLinePrefab, transform);
                connectLine.gameObject.SetActive(true);
                
                var distance = Vector2.Distance(currentPosition, targetPosition);
                connectLine.sizeDelta = new Vector2(distance, connectLine.sizeDelta.y);
                
                var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
                connectLine.localEulerAngles = new Vector3(0, 0, angle);
                
                // 親の位置を変更
                // Change the parent's position
                connectLine.SetParent(parent);
                
                // 親によってスケールが変わっている可能性があるので戻す
                // The scale may have changed due to the parent, so revert it
                connectLine.localScale = Vector3.one;
                
                // 接続線リストに追加
                // Add to connection line list
                _connectLines.Add(connectLine);
            }
            
  #endregion
        }
        
        public void SetStatus(ChallengeListUIElementState challengeListUIElementState)
        {
            CurrentState = challengeListUIElementState;
            
            completedObject.SetActive(challengeListUIElementState == ChallengeListUIElementState.Completed);
            currentObject.SetActive(challengeListUIElementState == ChallengeListUIElementState.Current);
            lockedObject.SetActive(challengeListUIElementState == ChallengeListUIElementState.Locked);
        }
        
        private void OnDestroy()
        {
            // 生成された接続線を削除
            // Destroy generated connection lines
            ClearConnectLines();
        }
        
        private void ClearConnectLines()
        {
            // 生成された接続線を削除
            // Destroy generated connection lines
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
