using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.BlockInventory.Logic.Common;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細（Web FluidInventoryContents）。行ごとに枠＋中身ありなら名前と「量 / 容量」
    /// Fluid details (Web FluidInventoryContents): per row a slot, plus name and "amount / capacity" when filled
    /// </summary>
    public sealed class FluidInventoryContents : VisualElement
    {
        private readonly UiStack _rows = new(UiGap.Xs);
        private readonly List<FluidInventoryRow> _rowViews = new();
        private readonly string _testid;
        private readonly GameContentPresentation _content;

        public FluidInventoryContents(string testid, GameContentPresentation content)
        {
            _testid = testid;
            _content = content;
            name = testid;
            AddToClassList("fluid-inventory-contents");
            Add(_rows);
        }

        public void Render(IReadOnlyList<BlockFluidSlotState> fluids)
        {
            // 行数が変わったときだけ組み直し、同じなら行を残して中身だけ差し替える（毎 tick の更新で要素を捨てない。D-37）
            // Rebuild only when the row count changes; otherwise keep the rows and swap contents (no element churn on per-tick updates; D-37)
            if (fluids.Count != _rowViews.Count) Rebuild(fluids.Count);
            for (var i = 0; i < fluids.Count; i++) _rowViews[i].Render(fluids[i]);

            #region Internal

            void Rebuild(int count)
            {
                _rows.ClearItems();
                _rowViews.Clear();
                for (var i = 0; i < count; i++)
                {
                    var row = new FluidInventoryRow($"{_testid}-slot-{i}", _content);
                    _rows.AddItem(row.Root);
                    _rowViews.Add(row);
                }
            }

            #endregion
        }
    }
}
