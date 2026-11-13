using System;
using Client.Localization;
using Client.Ui.Components.Recipe;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択の 1 行
    /// One selection row (Web MachineRecipeSelectionRow): name, badge, idle-arrow RecipeRow; selected rows get a 1px inner frame
    /// </summary>
    public sealed class MachineRecipeSelectionRowView : VisualElement
    {
        public const string SelectedClassName = "machine-recipe-selection-row--selected";

        private readonly Subject<Guid> _pressed = new();

        public IObservable<Guid> OnPressed => _pressed;

        public MachineRecipeSelectionRowView(MachineRecipeSelectionRow row, MachineRecipeData recipe, GameContentPresentation content, IUiOsCursor osCursor)
        {
            var recipeGuid = row.Recipe.RecipeGuid;
            var rowId = $"machine-recipe-{recipeGuid}";
            name = rowId;
            AddToClassList("machine-recipe-selection-row");
            SetSelected(row.Selected);

            // 名前→継承バッジ→静止矢印の行
            // Name → inheritance badge → idle-arrow row (no action column)
            var title = new LocalizedLabel { name = $"{rowId}-name", pickingMode = PickingMode.Ignore };
            title.AddToClassList("machine-recipe-selection-row__name");
            title.SetText(MachineRecipeNames.Row(row, content));
            Add(title);
            var badge = new LevelInheritanceBadge($"{rowId}-level-inheritance");
            if (LevelInheritanceBadgeText.TryBuild(recipe.LevelInheritance, out var badgeText)) badge.Show(badgeText);
            else badge.Hide();
            Add(badge);
            var duration = RecipeDurationText.Create(recipe.Time);
            var recipeRow = new RecipeRow($"{rowId}-row", $"{rowId}-arrow", duration);
            // 枠の押下は購読せず行が押す対象（Web と同じ）
            // Slot presses are not subscribed; the row itself is the press target (same as the Web selection row)
            recipeRow.SetSlots(MachineRecipeRowContentBuilder.Materials(rowId, recipe, content).Elements, MachineRecipeRowContentBuilder.Results(rowId, recipe, content).Elements);
            recipeRow.SetIdle();
            Add(recipeRow);

            // 選択中の外周は絶対配置の枠で描く
            // Selected outline is an absolute, non-picking frame; UITK has no outline and it adds no size
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.AddToClassList("machine-recipe-selection-row__outline");
            Add(outline);
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(recipeGuid));

            // 押せる印の指は要素の層で要求する
            // Whole-row pointer is requested on the element layer; USS cursor does nothing at runtime (web .row cursor: pointer)
            new UiOsCursorHoverRequest(this, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
        }

        public void SetSelected(bool selected)
        {
            // 並びが同じ再描画では行を残し、選択の印だけを当て直す（D-37）
            // On a same-order redraw the row stays and only the selection mark is reapplied (D-37)
            EnableInClassList(SelectedClassName, selected);
        }
    }
}
