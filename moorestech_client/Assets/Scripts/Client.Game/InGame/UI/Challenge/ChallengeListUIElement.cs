using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Util;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Challenge
{
    public class ChallengeListUIElement : MonoBehaviour
    {
        public Vector2 AnchoredPosition => rectTransform.anchoredPosition;
        public ChallengeMasterElement ChallengeMasterElement { get; private set; }
        
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private ItemSlotObject itemSlotObject;
        [SerializeField] private GameObject connectLinePrefab; // プレハブとして扱うGameObject
        
        [SerializeField] private GameObject currentObject;
        [SerializeField] private GameObject completedObject;
        
        [SerializeField] private UIEnterExplainerController uiEnterExplainerController;
        
        
        public void Initialize(ChallengeMasterElement challengeMasterElement)
        {
            ChallengeMasterElement = challengeMasterElement;
            
            SetUI();
            
            SetUIEnterExplain();
            
            #region Internal
            
            void SetUI()
            {
                var param = challengeMasterElement.DisplayListParam;
                // 位置の指定
                // Position specification
                rectTransform.anchoredPosition = param.UIPosition;
                
                // アイコンの指定
                // Icon specification
                if (param.IconItem != null)
                {
                    var itemView = ClientContext.ItemImageContainer.GetItemView(param.IconItem.Value);
                    itemSlotObject.SetItem(itemView, 0);
                }
                itemSlotObject.SetItemSlotObjectOption(new ItemSlotObjectBehaviourOption
                {
                    IsShowUIEnterExplain = false,
                });
            }
            
            void SetUIEnterExplain()
            {
                var clearedActionsTest = string.Empty;
                foreach (var clearedActionsElement in challengeMasterElement.ClearedActions)
                {
                    switch (clearedActionsElement.ClearedActionType)
                    {
                        case ClearedActionsElement.ClearedActionTypeConst.unlockCraftRecipe :
                            var recipeGuids = ((UnlockCraftRecipeClearedActionParam)clearedActionsElement.ClearedActionParam).UnlockRecipeGuids;
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
                        case ClearedActionsElement.ClearedActionTypeConst.unlockItemRecipeView :
                            var itemGuids = ((UnlockItemRecipeViewClearedActionParam)clearedActionsElement.ClearedActionParam).UnlockItemGuids;
                            clearedActionsTest += $"アイテム解放: ";
                            foreach (var guid in itemGuids)
                            {
                                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(guid);
                                clearedActionsTest += $"\n\t{itemMaster.Name}";
                            }
                            break;
                    }
                }
                
                
                var text = $"<size=30>{challengeMasterElement.Title}</size>\n\t{challengeMasterElement.Summary}\n\n達成報酬\n{clearedActionsTest}";
                uiEnterExplainerController.SetText(text, false);
            }
            
  #endregion
        }
        
        public void CreateConnect(Transform lineParent, Dictionary<Guid, ChallengeListUIElement> challengeListUIElements)
        {
            // 前のチャレンジがある場合、線を引く
            // If there is a previous challenge, draw a line
            var prevGuids = ChallengeMasterElement.PrevChallengeGuids;
            if (prevGuids == null) return;
            
            foreach (var prev in prevGuids)
            {
                if (!challengeListUIElements.TryGetValue(prev, out var prevChallengeListUIElement)) continue;
                
                // プレハブから線のインスタンスを生成
                var lineInstance = Instantiate(connectLinePrefab, lineParent);
                var lineRectTransform = lineInstance.GetComponent<RectTransform>();
                if (lineRectTransform == null)
                {
                    Debug.LogError("Connect line prefab does not have a RectTransform component.");
                    Destroy(lineInstance); // エラーの場合はインスタンスを破棄
                    continue;
                }
                
                // 線の長さと角度を計算して適用
                var currentPosition = AnchoredPosition;
                var targetPosition = prevChallengeListUIElement.AnchoredPosition;
                
                var distance = Vector2.Distance(currentPosition, targetPosition);
                lineRectTransform.sizeDelta = new Vector2(distance, lineRectTransform.sizeDelta.y);
                
                var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
                lineRectTransform.localEulerAngles = new Vector3(0, 0, angle);
                lineRectTransform.anchoredPosition = currentPosition; // 線の開始位置を現在の要素に合わせる
            }
        }
        
        public void SetStatus(ChallengeListUIElementState challengeListUIElementState)
        {
            completedObject.SetActive(challengeListUIElementState == ChallengeListUIElementState.Completed);
            currentObject.SetActive(challengeListUIElementState == ChallengeListUIElementState.Current);
        }
    }
    
    public enum ChallengeListUIElementState
    {
        Before,
        Current,
        Completed
    }
}