using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UnlockState;
using Client.Mod.Texture;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    //TODO CraftITemViewにリネーム
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        [SerializeField] private RectTransform craftResultParent;
        
        [SerializeField] private CraftButton craftButton;
        
        // 新規追加: レシピリスト用
        [SerializeField] private CraftRecipeItemElement craftRecipeItemElementPrefab;
        [SerializeField] private RectTransform craftRecipeListParent;
        
        [SerializeField] private TMP_Text itemNameText;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        private ItemSlotObject _craftResultSlot;
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        [Inject] private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private RecipeViewerItemRecipes _currentItemRecipes;
        private int _selectedRecipeIndex;
        
        [Inject]
        public void Construct()
        {
            _localPlayerInventory.OnItemChange.Subscribe(_ =>
            {
                if (_currentItemRecipes != null)
                {
                    var unlocked = _currentItemRecipes.UnlockedCraftRecipes();
                    if (_selectedRecipeIndex < unlocked.Count)
                    {
                        UpdateCraftButton(unlocked[_selectedRecipeIndex]);
                    }
                }
            });
        
            // クラフトボタン：選択中レシピでクラフト
            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (_currentItemRecipes == null)
                {
                    return;
                }
        
                var unlocked = _currentItemRecipes.UnlockedCraftRecipes();
                if (_selectedRecipeIndex < 0 || _selectedRecipeIndex >= unlocked.Count)
                    return;
        
                var currentCraftGuid = unlocked[_selectedRecipeIndex].CraftRecipeGuid;
                ClientContext.VanillaApi.SendOnly.Craft(currentCraftGuid);
            }).AddTo(this);
        }
        
        private void UpdateCraftButton(CraftRecipeMasterElement craftRecipe)
        {
            craftButton.SetInteractable(IsCraftable(craftRecipe));
        }
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            _currentItemRecipes = recipeViewerItemRecipes;
            _selectedRecipeIndex = 0;
        
            // 既存のレシピリストを全削除
            foreach (Transform child in craftRecipeListParent)
            {
                Destroy(child.gameObject);
            }
        
            var unlockedRecipes = _currentItemRecipes.UnlockedCraftRecipes();
            if (unlockedRecipes.Count == 0)
                return;
        
            // レシピリスト生成
            for (int i = 0; i < unlockedRecipes.Count; i++)
            {
                var recipe = unlockedRecipes[i];
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(recipe.CraftResultItemGuid);
                var icon = ClientContext.ItemImageContainer.GetItemView(recipe.CraftResultItemGuid)?.IconSprite;
                var recipeName = itemMaster?.Name ?? "???";
        
                var element = Instantiate(craftRecipeItemElementPrefab, craftRecipeListParent);
                element.SetRecipe(recipe, icon, recipeName);
                element.IsSelected = (i == _selectedRecipeIndex);
        
                int index = i; // キャプチャ用
                element.SetOnClick(_ =>
                {
                    UpdateRecipeSelection(index);
                });
            }
        
            // 最初のレシピでUI更新
            UpdateRecipeSelection(_selectedRecipeIndex);
        }
        
        // 選択状態の更新とUI反映
        private void UpdateRecipeSelection(int newIndex)
        {
            var unlockedRecipes = _currentItemRecipes.UnlockedCraftRecipes();
            if (unlockedRecipes.Count == 0) return;
        
            _selectedRecipeIndex = newIndex;
        
            // リストの選択状態を更新
            for (int i = 0; i < craftRecipeListParent.childCount; i++)
            {
                var element = craftRecipeListParent.GetChild(i).GetComponent<CraftRecipeItemElement>();
                if (element != null)
                    element.IsSelected = (i == _selectedRecipeIndex);
            }
        
            // 素材・結果スロット更新
            UpdateRecipeDetailUI(unlockedRecipes[_selectedRecipeIndex]);
        }
        
        // 素材・結果・ボタンUIの更新
        private void UpdateRecipeDetailUI(CraftRecipeMasterElement craftRecipe)
        {
            // 素材・結果スロットをクリア
            foreach (var materialSlot in _craftMaterialSlotList) Destroy(materialSlot.gameObject);
            _craftMaterialSlotList.Clear();
            if (_craftResultSlot != null) Destroy(_craftResultSlot.gameObject);
        
            // 素材スロット生成
            foreach (var requiredItem in craftRecipe.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
        
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                var toolTipText = GetMaterialTolTip(itemViewData);
                itemSlotObject.SetItem(itemViewData, requiredItem.Count, toolTipText);
                _craftMaterialSlotList.Add(itemSlotObject);
        
                // 原材料をクリックしたときにそのレシピを表示するようにする
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickMaterialItem);
            }
        
            // 結果スロット生成
            var resultItemViewData = ClientContext.ItemImageContainer.GetItemView(craftRecipe.CraftResultItemGuid);
            _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
            _craftResultSlot.SetItem(resultItemViewData, craftRecipe.CraftResultCount);
        
            // ボタン・テキスト更新
            craftButton.SetCraftTime(craftRecipe.CraftTime);
            UpdateCraftButton(craftRecipe);
        
            var itemName = MasterHolder.ItemMaster.GetItemMaster(craftRecipe.CraftResultItemGuid).Name;
            itemNameText.text = itemName;
        }
        
        // 原材料クリック時のレシピ切り替え
        private void OnClickMaterialItem(ItemSlotObject itemSlotObject)
        {
            var itemId = itemSlotObject.ItemViewData.ItemId;
            var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
            _onClickItem.OnNext(itemRecipes);
        }
        
        /// <summary>
        ///     そのレシピがクラフト可能かどうかを返す
        ///     この処理はある1つのレシピに対してのみ使い、一気にすべてのアイテムがクラフト可能かチェックするには<see cref="IsAllItemCraftable" />を用いる
        /// </summary>
        private bool IsCraftable(CraftRecipeMasterElement craftRecipeMasterElement)
        {
            var itemPerCount = new Dictionary<ItemId, int>();
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                if (itemPerCount.ContainsKey(item.Id))
                    itemPerCount[item.Id] += item.Count;
                else
                    itemPerCount.Add(item.Id, item.Count);
            }
            
            foreach (var requiredItem in craftRecipeMasterElement.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                
                if (!itemPerCount.ContainsKey(itemId)) return false;
                if (itemPerCount[itemId] < requiredItem.Count) return false;
            }
            
            return true;
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
        public static string GetMaterialTolTip(ItemViewData itemViewData)
        {
            var tooltipText = ItemSlotObject.GetToolTipText(itemViewData);
            var craftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(itemViewData.ItemId);
            
            // レシピがなければそのまま返す
            if (craftRecipes.Length == 0) return tooltipText;
            
            // レシピがあればテキストを追加
            tooltipText += $"\n<size=25>クリックでこのアイテムの\nレシピを確認</size>";
            
            return tooltipText;
        }
    }
}