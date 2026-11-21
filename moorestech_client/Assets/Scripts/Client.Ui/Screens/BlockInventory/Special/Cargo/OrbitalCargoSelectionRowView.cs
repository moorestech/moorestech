using System;
using System.Collections.Generic;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Recipe.Logic;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の一覧の 1 行: 積むものと返るものの文、積荷 → 1 便の秒 → 成果の RecipeRow、選択中の外周（前例 MachineRecipeSelectionRowView）
    /// One cargo row: the up/back text, a payload → seconds → outcome RecipeRow and the selected outline (precedent MachineRecipeSelectionRowView)
    /// </summary>
    public sealed class OrbitalCargoSelectionRowView : VisualElement
    {
        public const string SelectedClassName = "orbital-cargo-row--selected";

        private readonly Subject<ItemId> _pressed = new();

        public IObservable<ItemId> OnPressed => _pressed;

        public OrbitalCargoSelectionRowView(string rowId, OrbitalCargoChoice choice, double flightSeconds, GameContentPresentation content, IUiOsCursor osCursor)
        {
            focusable = false;
            name = rowId;
            AddToClassList("orbital-cargo-row");

            // 文 → 静止矢印の行（枠の押下は購読せず、行が押す対象）
            // Text → idle-arrow row; slot presses are not subscribed, the row is the press target
            var title = new LocalizedLabel { name = $"{rowId}-name", pickingMode = PickingMode.Ignore };
            title.AddToClassList("orbital-cargo-row__name");
            title.SetText(OrbitalCargoNames.Row(choice, content));
            Add(title);
            var recipeRow = new RecipeRow($"{rowId}-row", $"{rowId}-arrow", RecipeDurationText.Create(flightSeconds));
            recipeRow.SetSlots(new[] { Slot($"{rowId}-payload", choice.PayloadItemId, choice.CountPerLaunch) }, Outputs());
            recipeRow.SetIdle();
            Add(recipeRow);

            // 選択中の外周は絶対配置の枠で描く
            // The selected outline is an absolute, non-picking frame
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.AddToClassList("orbital-cargo-row__outline");
            Add(outline);
            RegisterCallback<ClickEvent>(_ => _pressed.OnNext(choice.PayloadItemId));
            osCursor.CreateHoverRequest(this, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);

            #region Internal

            // 右は出力の品を全部並べる。プールへ足すだけの種類は右が空で、文が「ケーブルプール +1000」を語る
            // The right lists every output item; a pool-only kind leaves it empty and the text says "Cable pool +1000"
            IReadOnlyList<VisualElement> Outputs()
            {
                var slots = new List<VisualElement>(choice.ArrivalOutputs.Count);
                for (var i = 0; i < choice.ArrivalOutputs.Count; i++) slots.Add(Slot($"{rowId}-output-{i}", choice.ArrivalOutputs[i].ItemId, choice.ArrivalOutputs[i].Count));
                return slots;
            }

            VisualElement Slot(string testid, ItemId itemId, int count)
            {
                var slot = new ItemSlot(testid);
                slot.SetContent(SlotItemContent.ForItem(content, itemId, count));
                return slot;
            }

            #endregion
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList(SelectedClassName, selected);
        }
    }
}
