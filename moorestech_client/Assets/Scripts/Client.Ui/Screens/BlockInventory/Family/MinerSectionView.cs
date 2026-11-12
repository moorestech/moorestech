using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Family;
using Core.Master;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 採掘機の区画
    /// The miner section (Web MinerSection): progress bar → power rate → per-minute per mined item
    /// </summary>
    public sealed class MinerSectionView : IDetailSectionView<MinerDetail>
    {
        private readonly UiStack _root = new(UiGap.Xs) { name = "miner-section" };
        private readonly ProgressArrowBar _progress = new();
        private readonly PowerRateText _powerRate = new("miner-power-rate");
        private readonly UiGroup _items = new(UiGap.Xs) { name = "miner-mining-items" };
        private readonly KeyedRowList<ItemId, PerMinuteRateRow> _rows = new();
        private readonly GameContentPresentation _content;

        public VisualElement Root => _root;

        public MinerSectionView(GameContentPresentation content)
        {
            _content = content;
            _root.AddItem(_progress);
            _root.AddItem(_powerRate);
            _root.AddItem(_items);

            // 採掘機でないブロックでは描かれないまま載るので、文の無いラベルが LogError にならないよう文を先に入れる
            // On non-miner blocks this is mounted without ever being drawn, so seed the text to keep the text-less label from logging an error
            _powerRate.Render(0, 0, false);
        }

        public void Render(MinerDetail detail)
        {
            // 歯車採掘機の不足赤は網の行が担う
            // Gear miners leave shortage red to the network stop-reason row (ADR 0056)
            _progress.SetValue(detail.Progress);
            _powerRate.Render(detail.CurrentPower, detail.RequestPower, !detail.GearDriven);

            // 採掘中の品の並びが同じ間は行を残して毎分だけ描き直す（採掘中は毎 tick 届くので要素を捨てない。D-37）
            // Keep the rows and redraw only the rate while the mined items are the same (updates arrive every tick while mining; D-37)
            if (!_rows.HasSameKeys(ItemIds())) Rebuild();
            for (var i = 0; i < _rows.Count; i++) _rows.RowAt(i).Render(detail.MiningItems[i].ItemsPerMinute);

            #region Internal

            ItemId[] ItemIds()
            {
                var ids = new ItemId[detail.MiningItems.Count];
                for (var i = 0; i < ids.Length; i++) ids[i] = detail.MiningItems[i].ItemId;
                return ids;
            }

            void Rebuild()
            {
                _items.ClearItems();
                _rows.Clear();
                foreach (var item in detail.MiningItems)
                {
                    // Web の採掘機の ItemSlot は testid を持たないが、UITK の枠は名前が要るので録画・テストの照準用に付ける
                    // The Web miner ItemSlot has no testid, but the UITK frame needs a name, so add one as an aim for recordings and tests
                    var slot = new ItemSlot($"miner-mining-item-{item.ItemId.AsPrimitive()}");
                    slot.SetContent(new ItemSlotContent(_content.Item(item.ItemId), null, new ItemSlotMarks(false, false, false), null, null, null));
                    var row = new PerMinuteRateRow(slot);
                    _items.AddItem(row);
                    _rows.Add(item.ItemId, row);
                }
            }

            #endregion
        }
    }
}
