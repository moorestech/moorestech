using System;
using System.Globalization;
using Client.Localization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// インベントリモード上部の選択中レシピ（Web SelectedRecipeHeader）。代表の枠 → 名前 → 秒。押すとレシピ選択モードへ戻る
    /// The selected recipe atop inventory mode (Web SelectedRecipeHeader): representative slot → name → seconds; pressing returns to recipe selection
    /// </summary>
    public sealed class SelectedRecipeHeader : VisualElement
    {
        private static readonly ItemSlotMarks NoMarks = new(false, false, false);

        private readonly UiGroup _group = new(UiGap.Xs);
        private readonly Subject<Unit> _pressed = new();
        private readonly GameContentPresentation _content;
        private bool _hasShownRecipe;
        private Guid _shownRecipeGuid;

        public IObservable<Unit> OnPressed => _pressed;

        public SelectedRecipeHeader(GameContentPresentation content)
        {
            name = "machine-selected-recipe";
            AddToClassList("machine-selected-recipe");
            _content = content;
            _group.pickingMode = PickingMode.Ignore;
            _group.AddToClassList(UiLayoutClass.GroupJustifyCenter);
            Add(_group);
            HoverTooltipMark.Mark(this, new UiText(LocalizationKeys.Ui.BlockInventory.ChangeRecipe));
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(Unit.Default));
        }

        public void Render(MachineRecipeSelectionRow row, MachineSectionViewModel viewModel)
        {
            // 選択中のレシピが前回と同じなら描き直さない（状態更新ごとに作り直さない。D-37）
            // Skip redrawing while the selected recipe is unchanged (not rebuilt per state update; D-37)
            var recipeGuid = row.Recipe.RecipeGuid;
            if (_hasShownRecipe && _shownRecipeGuid == recipeGuid) return;
            _hasShownRecipe = true;
            _shownRecipeGuid = recipeGuid;

            _group.ClearItems();
            _group.AddItem(RepresentativeSlot(row.Subject));
            var nameLabel = new LocalizedLabel { name = "machine-selected-recipe-name", pickingMode = PickingMode.Ignore };
            nameLabel.SetText(MachineRecipeNames.Representative(row.Subject, _content));
            _group.AddItem(nameLabel);
            AddDuration();

            #region Internal

            void AddDuration()
            {
                // 行はカタログから組まれるので必ず引ける。引けなければ組み立ての誤りとして知らせ、秒を出さない
                // Rows are built from the catalog so the lookup always succeeds; a miss is a build bug, reported, and the seconds are omitted
                if (!viewModel.TryGetRecipe(recipeGuid, out var recipe))
                {
                    Debug.LogError($"[SelectedRecipeHeader] recipe {recipeGuid} of the selected row is missing from the catalog; the duration is not shown");
                    return;
                }
                var time = new LocalizedLabel { name = "machine-selected-recipe-time", pickingMode = PickingMode.Ignore };
                time.AddToClassList(UiTypographyClass.TextSm);
                time.AddToClassList(UiTypographyClass.TextDimmed);
                time.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.RecipeDuration, new[] { new LocalizationTextParam("seconds", recipe.Time.ToString(CultureInfo.InvariantCulture)) }));
                _group.AddItem(time);
            }

            #endregion
        }

        private VisualElement RepresentativeSlot(RecipeDisplaySubject subject)
        {
            // 代表はアイテムなら ItemSlot、液体なら量の帯を出さない液体の枠（Web は同じ枠の大きさ）
            // An item representative uses ItemSlot, a fluid one a fluid frame without the amount band (same frame size on the Web)
            switch (subject)
            {
                case ItemRecipeDisplaySubject item:
                    var itemSlot = new ItemSlot("machine-selected-recipe-item");
                    itemSlot.SetContent(new ItemSlotContent(_content.Item(item.ItemId), null, NoMarks, null, null, null));
                    return itemSlot;
                case FluidRecipeDisplaySubject fluid:
                    var fluidSlot = new FluidAmountSlot("machine-selected-recipe-fluid");
                    fluidSlot.SetFluid(_content.Fluid(fluid.FluidId), SlotAmountBadge.Hidden);
                    return fluidSlot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subject), subject, "RecipeDisplaySubject has only the item and fluid kinds");
            }
        }
    }
}
