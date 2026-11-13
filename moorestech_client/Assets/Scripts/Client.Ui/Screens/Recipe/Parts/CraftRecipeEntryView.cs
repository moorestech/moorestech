using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.State.MaterialTooltip;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// クラフトの 1 件（Web CraftRecipeEntry の写し）。素材・結果・ボタン・長押しを持ち、描き直しでは要素を作り直さない
    /// One craft entry (port of the Web CraftRecipeEntry); owns materials, result, button and hold, and never rebuilds on redraw
    /// </summary>
    public sealed class CraftRecipeEntryView : VisualElement
    {
        private const string MaterialSlotClass = "craft-recipe-entry__material";
        private readonly RecipeEntryPartsContext _context;
        private readonly RecipeActionButton _button;
        private readonly CraftHoldDriver _hold;
        private readonly ItemSlot[] _materialSlots;
        private readonly ItemId[] _materialItemIds;
        private readonly ItemSlot _resultSlot;
        private bool _anchored;

        public CraftRecipeEntryView(CraftRecipeEntryViewModel first, RecipeEntryPartsContext context)
        {
            _context = context;
            var guid = first.RecipeGuid;
            name = $"craft-recipe-entry-{guid}";

            // 行・秒数・ボタン（秒数は行の ctor で受ける。A-196）
            // Row, seconds and button (the row takes the seconds in its ctor; A-196)
            var row = new RecipeRow($"craft-recipe-box-{guid}", $"craft-progress-arrow-{guid}", RecipeDurationText.Create(first.Recipe.CraftTime));
            _button = new RecipeActionButton($"craft-button-{guid}", new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), RecipeActionButtonVariant.Craft);
            row.SetAction(_button);

            // Enter の長押しのためこのインスタンスだけフォーカスを持ち、規則に外されない印を付ける（部品の既定 focusable=false の画面側の例外。D-31・D-32）
            // Only this instance takes focus for the Enter hold and is marked so the rules keep it (screen-side exception to the component default; D-31, D-32)
            _button.focusable = true;
            UiKeyFocusKeepMark.Mark(_button);
            _hold = new CraftHoldDriver(this, row, context.Commands, context.Toaster);
            new CraftHoldInput(_button, _hold);

            // 素材と結果のスロットは件数が GUID で決まるので 1 回だけ作る
            // Material and result slots are created once since the GUID fixes their count
            _materialSlots = new ItemSlot[first.Materials.Count];
            _materialItemIds = new ItemId[first.Materials.Count];
            for (var i = 0; i < _materialSlots.Length; i++) _materialSlots[i] = CreateMaterialSlot(guid, i);
            _resultSlot = new ItemSlot($"craft-recipe-box-{guid}-result");
            row.SetSlots(_materialSlots, new VisualElement[] { _resultSlot });
            Add(row);
            Render(first);

            #region Internal

            ItemSlot CreateMaterialSlot(Guid guid, int index)
            {
                // 素材の左押下はその素材のレシピへ移る（Web onLeftDown）。購読は ItemSlot 自身の Subject に閉じるので要素と一緒に捨てる
                // A left press on a material moves to that material's recipes (the Web onLeftDown); the subscription stays on the slot's own Subject and goes with the element
                var slot = new ItemSlot($"craft-recipe-box-{guid}-material-{index}");
                slot.AddToClassList(MaterialSlotClass);
                slot.OnSlotGestured.Subscribe(gesture => OnMaterialGestured(index, gesture));
                return slot;
            }

            void OnMaterialGestured(int index, SlotGesture gesture)
            {
                // silent-ok: 素材は左押下だけを受ける（Web は onLeftDown だけ渡す）/ CraftRecipeEntryViewTest の左押下の件が見る
                if (gesture.Kind != SlotGestureKind.LeftDown) return;
                _context.Selection.Select(_materialItemIds[index]);
            }

            #endregion
        }

        public void Render(CraftRecipeEntryViewModel vm)
        {
            // アンカーは先頭のクラフトだけ。解放で先頭が変わったら付け替え、要素は作り直さない（長押しを保つ。Web は key を保って属性だけ変える。D-36）
            // Only the first craft carries the anchor; on an unlock move it without rebuilding (keeps the hold; Web keeps the key and updates the attribute; D-36)
            if (vm.CarriesTutorialAnchor != _anchored)
            {
                if (vm.CarriesTutorialAnchor) TutorialAnchor.Mark(_button, AnchorIds.RecipeCraftButton);
                else TutorialAnchor.Unmark(_button, AnchorIds.RecipeCraftButton);
                _anchored = vm.CarriesTutorialAnchor;
            }

            // 素材の所持・不足とツールチップ、結果の数、ボタンの可否、長押しの可否を当て直す
            // Re-apply material owned/lacking and tooltips, result count, button state and hold eligibility
            for (var i = 0; i < _materialSlots.Length; i++) ApplyMaterial(i, vm.Materials[i]);
            _resultSlot.SetContent(SlotItemContent.ForItem(_context.Content, vm.Recipe.ResultItemId, vm.Recipe.ResultCount));
            _button.SetEnabled(vm.Craftable);
            _hold.SetRecipe(vm.RecipeGuid, vm.Recipe.CraftTime, vm.Craftable);

            #region Internal

            void ApplyMaterial(int index, RecipeMaterialViewModel material)
            {
                // 数は所持/必要の表示が描くのでバッジの数は渡さない（Web の素材の ItemSlot は count を持たない）
                // The owned/required text draws the numbers, so no badge count is passed (the Web material ItemSlot has no count)
                _materialItemIds[index] = material.ItemId;
                _materialSlots[index].SetContent(SlotItemContent.ForMaterial(_context.Content, material.ItemId, MaterialTooltipKind.RecipeMaterial, material.Owned, material.Required, material.Lacking));
            }

            #endregion
        }

    }

    /// <summary>
    /// エントリの包みが共通で受ける依存の束
    /// Bundle of dependencies every entry wrapper receives
    /// </summary>
    public sealed class RecipeEntryPartsContext
    {
        public GameContentPresentation Content { get; }
        public IRecipeCommands Commands { get; }
        public UiCommandResultToaster Toaster { get; }
        public RecipeItemSelection Selection { get; }

        public RecipeEntryPartsContext(GameContentPresentation content, IRecipeCommands commands, UiCommandResultToaster toaster, RecipeItemSelection selection)
        {
            Content = content;
            Commands = commands;
            Toaster = toaster;
            Selection = selection;
        }
    }
}
