using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Mod.Texture; // ItemViewDataのために追加
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class RecipeListItem : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color selectedColor = Color.cyan;
        [SerializeField] private Color deselectedColor = Color.white;

        [SerializeField] private ItemSlotObject resultItemSlot;
        [SerializeField] private RectTransform materialItemParent;
        [SerializeField] private ItemSlotObject materialItemSlotPrefab; // 素材アイテム表示用のPrefab

        [SerializeField] private TMP_Text recipeNameText; // レシピ名（結果アイテム名）表示用

        public IObservable<RecipeListItem> OnClickItem => _onClickItem;
        private readonly Subject<RecipeListItem> _onClickItem = new();

        public CraftRecipeMasterElement RecipeData { get; private set; }

        private readonly List<ItemSlotObject> _materialSlots = new();

        public void SetRecipe(CraftRecipeMasterElement recipeData)
        {
            RecipeData = recipeData;

            // 結果アイテムの設定
            var resultItemViewData = ClientContext.ItemImageContainer.GetItemView(recipeData.CraftResultItemGuid);
            resultItemSlot.SetItem(resultItemViewData, recipeData.CraftResultCount);
            resultItemSlot.OnLeftClickUp.Subscribe(_ => _onClickItem.OnNext(this)); // 結果アイテムクリックでも選択

            // レシピ名（結果アイテム名）の設定
            if (recipeNameText != null)
            {
                recipeNameText.text = MasterHolder.ItemMaster.GetItemMaster(recipeData.CraftResultItemGuid).Name;
            }

            // 素材アイテムの設定
            // 古い素材スロットをクリア
            foreach (var slot in _materialSlots)
            {
                Destroy(slot.gameObject);
            }
            _materialSlots.Clear();

            // 新しい素材スロットを作成
            foreach (var requiredItem in recipeData.RequiredItems)
            {
                var materialItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var materialItemViewData = ClientContext.ItemImageContainer.GetItemView(materialItemId);

                var materialSlot = Instantiate(materialItemSlotPrefab, materialItemParent);
                // ツールチップ生成ロジックをここに移動
                var toolTipText = GetMaterialToolTip(materialItemViewData);
                materialSlot.SetItem(materialItemViewData, requiredItem.Count, toolTipText);
                _materialSlots.Add(materialSlot);

                // 素材アイテムクリックでそのアイテムのレシピを表示するイベントを発行（必要なら）
                // materialSlot.OnLeftClickUp.Subscribe(clickedSlot => { /* ItemRecipeViewerに通知する処理 */ });
            }

            SetSelected(false); // 初期状態は非選択
        }

        public void SetSelected(bool isSelected)
        {
            backgroundImage.color = isSelected ? selectedColor : deselectedColor;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _onClickItem.OnNext(this);
            }
        }

        private void OnDestroy()
        {
            _onClickItem.Dispose();
        }

        // 素材アイテム用のツールチップテキストを生成するメソッド
        private string GetMaterialToolTip(ItemViewData itemViewData)
        {
            // ItemSlotObject.GetToolTipText が static かどうかで呼び出し方を調整
            // ここでは static であると仮定
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