using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.FilterSplitter
{
    /// <summary>
    /// フィルタ分配器の本文。方向を縦に積む（Web FilterSplitterInventory。詳細の未着は何も出さない）
    /// Filter splitter body stacking directions vertically (Web FilterSplitterInventory; nothing before the detail arrives)
    /// </summary>
    public sealed class FilterSplitterBodyView : VisualElement, ISpecialDetailRenderer
    {
        private readonly SpecialViewKit _kit;
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly List<FilterDirectionView> _directions = new();

        public FilterSplitterBodyView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "filter-splitter";
            AddToClassList("filter-splitter");
            Add(_stack);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            var present = detail.TryGetFilterSplitter(out var filter);
            SpecialViewParts.SetShown(_stack, present);
            // silent-ok: 詳細の未着は本文を空のまま（Web と同じ） / 届かないままなら録画（R-12）の振り分け機の段で気づく
            if (!present) return;

            // 方向の数が変わったときだけ作り直す（開いている間はマスタで固定）
            // Rebuild only when the direction count changes (fixed by the master while open)
            if (_directions.Count != filter.Directions.Count)
            {
                _stack.ClearItems();
                _directions.Clear();
                for (var d = 0; d < filter.Directions.Count; d++)
                {
                    var direction = new FilterDirectionView(_kit, d);
                    _directions.Add(direction);
                    _stack.AddItem(direction);
                }
            }
            for (var d = 0; d < _directions.Count; d++) _directions[d].Render(filter.Directions[d]);
        }
    }
}
