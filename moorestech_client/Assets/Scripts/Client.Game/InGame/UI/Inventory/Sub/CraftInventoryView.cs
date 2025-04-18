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
        // --- 旧UI要素（ページング関連）はコメントアウトまたは削除 ---
        // [SerializeField] private ItemSlotObject itemSlotObjectPrefab; // RecipeListItem内で使用
        // [SerializeField] private RectTransform craftMaterialParent; // RecipeListItem内で使用
        // [SerializeField] private RectTransform craftResultParent; // RecipeListItem内で使用
        // [SerializeField] private Button nextRecipeButton;
        // [SerializeField] private Button prevRecipeButton;
        // [SerializeField] private TMP_Text recipeCountText;
        // [SerializeField] private TMP_Text itemNameText; // 必要なら選択中レシピ表示に使う

        [SerializeField] private CraftButton craftButton;
        [SerializeField] private RectTransform recipeListParent; // ScrollViewのContent
        [SerializeField] private RecipeListItem recipeListItemPrefab; // レシピリスト要素のPrefab
        [SerializeField] private TMP_Text itemNameText; // 選択中のレシピ名表示用（任意）

        // ItemSlotObjectのPrefabはRecipeListItemから参照するため、ここでは不要

        // --- 削除または変更 ---
        // public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        // private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        // private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        // private ItemSlotObject _craftResultSlot;
        // private int CraftRecipeCount => _currentItemRecipes?.UnlockedCraftRecipes().Count ?? 0; // 不要
        // private RecipeViewerItemRecipes _currentItemRecipes; // 不要
        // private int _currentIndex; // 不要

        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        [Inject] private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer; // 素材クリック時のレシピ表示に必要なら残す

        private RecipeListItem _selectedRecipeItem; // 現在選択中のレシピリストアイテム
        private readonly List<RecipeListItem> _currentRecipeListItems = new(); // 表示中のリストアイテム管理用

        [Inject]
        public void Construct()
        {
            _localPlayerInventory.OnItemChange.Subscribe(_ =>
            {
                // 選択中のレシピがあればクラフトボタンの状態を更新
                if (_selectedRecipeItem != null)
                {
                    UpdateCraftButton(_selectedRecipeItem.RecipeData);
                }
            }).AddTo(this);

            // --- ページングボタンのリスナー削除 ---
            // nextRecipeButton.onClick.AddListener(...)
            // prevRecipeButton.onClick.AddListener(...)

            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                // 選択中のレシピがなければ何もしない
                if (_selectedRecipeItem == null)
                {
                    Debug.LogWarning("クラフトするレシピが選択されていません。");
                    return;
                }

                // 選択中のレシピでクラフトを実行
                var craftGuid = _selectedRecipeItem.RecipeData.CraftRecipeGuid;
                ClientContext.VanillaApi.SendOnly.Craft(craftGuid);
            }).AddTo(this);
        }

        private void UpdateCraftButton(CraftRecipeMasterElement craftRecipe)
        {
            if (craftRecipe == null)
            {
                craftButton.SetInteractable(false);
                craftButton.SetCraftTime(0); // クラフト時間もリセット
                return;
            }
            craftButton.SetInteractable(IsCraftable(craftRecipe));
            craftButton.SetCraftTime(craftRecipe.CraftTime);
        }

        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            // 古いリストアイテムをクリア
            foreach (var item in _currentRecipeListItems)
            {
                Destroy(item.gameObject);
            }
            _currentRecipeListItems.Clear();
            _selectedRecipeItem = null; // 選択状態もクリア
            UpdateCraftButton(null); // クラフトボタンもリセット
            if (itemNameText != null) itemNameText.text = ""; // レシピ名もクリア


            if (recipeViewerItemRecipes == null)
            {
                // レシピがない場合は何もしない
                return;
            }

            var unlockedRecipes = recipeViewerItemRecipes.UnlockedCraftRecipes();
            if (unlockedRecipes.Count == 0)
            {
                // アンロック済みレシピがない場合も何もしない
                return;
            }

            // 新しいリストアイテムを作成
            foreach (var recipeData in unlockedRecipes)
            {
                var listItem = Instantiate(recipeListItemPrefab, recipeListParent);
                listItem.SetRecipe(recipeData);
                listItem.OnClickItem.Subscribe(HandleRecipeItemClick).AddTo(listItem); // DisposeされるようにAddToする
                _currentRecipeListItems.Add(listItem);
            }

            // デフォルトで最初のレシピを選択状態にする（任意）
            if (_currentRecipeListItems.Count > 0)
            {
                HandleRecipeItemClick(_currentRecipeListItems[0]);
            }
        }

        private void HandleRecipeItemClick(RecipeListItem clickedItem)
        {
            if (_selectedRecipeItem == clickedItem)
            {
                // すでに選択されているアイテムをクリックした場合は何もしない（または選択解除のロジック）
                return;
            }

            // 前に選択されていたアイテムの選択を解除
            if (_selectedRecipeItem != null)
            {
                _selectedRecipeItem.SetSelected(false);
            }

            // 新しいアイテムを選択状態にする
            _selectedRecipeItem = clickedItem;
            _selectedRecipeItem.SetSelected(true);

            // クラフトボタンの状態を更新
            UpdateCraftButton(_selectedRecipeItem.RecipeData);

            // 選択されたレシピ名を表示（任意）
            if (itemNameText != null)
            {
                 itemNameText.text = MasterHolder.ItemMaster.GetItemMaster(_selectedRecipeItem.RecipeData.CraftResultItemGuid).Name;
            }
        }


        // --- DisplayRecipeメソッドは削除 ---
        // public void DisplayRecipe(int index) { ... }


        /// <summary>
        ///     そのレシピがクラフト可能かどうかを返す
        /// </summary>
        private bool IsCraftable(CraftRecipeMasterElement craftRecipeMasterElement)
        {
             if (craftRecipeMasterElement == null) return false; // nullチェック追加

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
            if (!isActive)
            {
                // 非アクティブになったら選択状態をリセットする（任意）
                if (_selectedRecipeItem != null)
                {
                    _selectedRecipeItem.SetSelected(false);
                    _selectedRecipeItem = null;
                }
                UpdateCraftButton(null);
                 if (itemNameText != null) itemNameText.text = "";
            }
        }

        // GetMaterialTolTipメソッドはRecipeListItem.csに移動したため削除
    }
}