using System;
using Client.Localization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 選択中レシピのヘッダ
    /// Selected recipe atop inventory mode (Web SelectedRecipeHeader): slot → name → seconds; pressing returns to selection
    /// </summary>
    public sealed class SelectedRecipeHeader : VisualElement
    {
        private readonly UiGroup _group = new(UiGap.Xs);
        private readonly Subject<Unit> _pressed = new();
        private readonly GameContentPresentation _content;
        private bool _hasShownRecipe;
        private Guid _shownRecipeGuid;

        public IObservable<Unit> OnPressed => _pressed;

        public SelectedRecipeHeader(GameContentPresentation content, IUiOsCursor osCursor)
        {
            name = "machine-selected-recipe";
            AddToClassList("machine-selected-recipe");
            _content = content;
            _group.pickingMode = PickingMode.Ignore;
            _group.AddToClassList(UiLayoutClass.GroupJustifyCenter);
            Add(_group);
            HoverTooltipMark.Mark(this, new UiText(LocalizationKeys.Ui.BlockInventory.ChangeRecipe));
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(Unit.Default));

            // 押せる印の指は USS の cursor がランタイムで効かないので OS カーソルへ要素の層で要求する（web: SelectedRecipeHeader の cursor: pointer）
            // USS cursor does nothing at runtime, so the pressable pointer is requested on the element layer (web: SelectedRecipeHeader cursor: pointer)
            new UiOsCursorHoverRequest(this, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
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
                time.SetText(RecipeDurationText.Create(recipe.Time));
                _group.AddItem(time);
            }

            VisualElement RepresentativeSlot(RecipeDisplaySubject subject)
            {
                // 代表は ItemSlot か液体の枠
                // An item representative uses ItemSlot, a fluid one a fluid frame without the amount band, same size as Web
                switch (subject)
                {
                    case ItemRecipeDisplaySubject item:
                        var itemSlot = new ItemSlot("machine-selected-recipe-item");
                        itemSlot.SetContent(SlotItemContent.ForItem(_content, item.ItemId, null));
                        return itemSlot;
                    case FluidRecipeDisplaySubject fluid:
                        var fluidSlot = new FluidAmountSlot("machine-selected-recipe-fluid");
                        fluidSlot.SetFluid(_content.Fluid(fluid.FluidId), SlotAmountBadge.Hidden);
                        return fluidSlot;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(subject), subject, "RecipeDisplaySubject has only the item and fluid kinds");
                }
            }

            #endregion
        }
    }
}
