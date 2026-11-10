using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細の 1 行。要素は作った後は替えず、Render で枠の中身と文と文の出し入れだけを当て直す
    /// One fluid-details row; elements are fixed after construction and Render only reapplies slot contents, texts and text visibility
    /// </summary>
    public sealed class FluidInventoryRow
    {
        private readonly UiGroup _group = new(UiGap.Sm);
        private readonly FluidSlotRow _slotRow;
        private readonly UiStack _texts = new(UiGap.None);
        private readonly LocalizedLabel _name = Text();
        private readonly LocalizedLabel _amount = Text();
        private readonly GameContentPresentation _content;

        public VisualElement Root => _group;

        public FluidInventoryRow(string slotTestid, GameContentPresentation content)
        {
            // 行は Group gap=sm・中央揃え・折り返しなし（揃えと折り返しは内側の行に効く UiLayoutClass で付ける）
            // Each row is Group gap=sm, centered, no wrap (set via UiLayoutClass, which reaches the inner row)
            _content = content;
            _group.AddToClassList(UiLayoutClass.GroupNoWrap);
            _slotRow = new FluidSlotRow(slotTestid);
            _group.AddItem(_slotRow);
            _texts.AddItem(_name);
            _texts.AddItem(_amount);
            _group.AddItem(_texts);
        }

        public void Render(BlockFluidSlotState fluid)
        {
            // 空の液体は枠だけ（名前と容量を出さない。Web と同じ）
            // An empty fluid shows only the slot (no name or capacity, same as Web)
            _slotRow.SetFluids(new[] { FluidSlotContentMapper.Map(fluid, _content) });
            var filled = fluid as FilledBlockFluidSlotState;
            _group.SetItemShown(_texts, filled != null);
            if (filled == null) return;
            _name.SetText(_content.Fluid(filled.FluidId).Label);
            _amount.SetText(UiText.FromVerbatim($"{SlotAmountFormat.Format(filled.Amount)} / {SlotAmountFormat.Format(filled.Capacity)}"));
        }

        private static LocalizedLabel Text()
        {
            // 空の液体の行では文を一度も当てないので、載せたときの「文なし」の報告を避けるため空の原文で初期化する
            // An empty-fluid row never sets text, so start from an empty verbatim text to avoid the attached-without-text report
            var label = new LocalizedLabel();
            label.SetText(UiText.FromVerbatim(string.Empty));
            label.AddToClassList(UiTypographyClass.TextSm);
            return label;
        }
    }
}
