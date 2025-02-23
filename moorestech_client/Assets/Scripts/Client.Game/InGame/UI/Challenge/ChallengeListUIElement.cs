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
        [SerializeField] private RectTransform connectLineParent;
        
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
                var param = (DisplayDisplayListParam)challengeMasterElement.DisplayListParam;
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
                            var recipeGuid = ((UnlockCraftRecipeClearedActionParam)clearedActionsElement.ClearedActionParam).UnlockRecipeGuid;
                            var recipe = MasterHolder.CraftRecipeMaster.GetCraftRecipe(recipeGuid);
                            var unlockRecipeText = "レシピ解放: \n\t";
                            foreach (var requiredItem in recipe.RequiredItems)
                            {
                                var requiredItemMaster = MasterHolder.ItemMaster.GetItemMaster(requiredItem.ItemGuid);
                                unlockRecipeText += $"{requiredItemMaster.Name}x{requiredItem.Count} ";
                            }
                            var resultItemMaster = MasterHolder.ItemMaster.GetItemMaster(recipe.CraftResultItemGuid);
                            unlockRecipeText += $"\n\t=> {resultItemMaster.Name}x{recipe.CraftResultCount}";
                            
                            clearedActionsTest += unlockRecipeText;
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
            // 線のオブジェクトをオフにしておく
            // Turn off the line object
            connectLineParent.gameObject.SetActive(false);
            
            // 前のチャレンジがある場合、線を引く
            // If there is a previous challenge, draw a line
            var prev = ChallengeMasterElement.PrevChallengeGuid;
            if (!prev.HasValue) return;
            if (!challengeListUIElements.TryGetValue(prev.Value, out var prevChallengeListUIElement)) return;
            
            // 線の長さと角度を計算して適用
            // Calculate and apply the length and angle of the line
            var currentPosition = AnchoredPosition;
            var targetPosition = prevChallengeListUIElement.AnchoredPosition;
            
            connectLineParent.gameObject.SetActive(true);
            var distance = Vector2.Distance(currentPosition, targetPosition);
            connectLineParent.sizeDelta = new Vector2(distance, connectLineParent.sizeDelta.y);
            
            var angle = Mathf.Atan2(targetPosition.y - currentPosition.y, targetPosition.x - currentPosition.x) * Mathf.Rad2Deg;
            connectLineParent.localEulerAngles = new Vector3(0, 0, angle);
            
            // 親の位置を変更
            // Change the parent's position
            connectLineParent.SetParent(lineParent);
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