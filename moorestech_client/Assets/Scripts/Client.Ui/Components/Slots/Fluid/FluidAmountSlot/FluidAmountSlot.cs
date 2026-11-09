using System;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 容量なしの液体 1 マス（白面の枠＋絵＋量の帯）
    /// One capacity-less fluid cell: the item's white frame with a picture and an amount band (the Web FluidAmountSlot)
    /// </summary>
    public sealed class FluidAmountSlot : VisualElement
    {
        private readonly SlotFrame _frame;
        private readonly SlotContent _content;
        private readonly VisualElement _amountBand;
        private readonly OutlinedCountLabel _amount;

        public IObservable<SlotGesture> OnSlotGestured => _frame.OnSlotGestured;

        public FluidAmountSlot(string frameTestid)
        {
            // testid・印・ポインタは枠が持つ
            // The frame owns the testid, mark and pointer, as on the Web
            pickingMode = PickingMode.Ignore;
            AddToClassList("fluid-amount-slot");
            _frame = new SlotFrame(frameTestid);
            _content = new SlotContent();
            _frame.Add(_content);
            Add(_frame);

            // 量の帯は枠幅の右寄せで、testid を持つので出さないときは木から外す（Web は要素ごと描かない）
            // The amount band is right-aligned across the frame and carries a testid, so it leaves the tree when hidden (the Web renders nothing)
            _amountBand = new VisualElement { name = frameTestid + "-amount", pickingMode = PickingMode.Ignore };
            _amountBand.AddToClassList("fluid-amount-slot__amount");
            _amount = new OutlinedCountLabel(_amountBand, "fluid-amount-slot__amount-layer", UiTypographyClass.TextMedium);
        }

        public void SetFluid(UiIcon icon, SlotAmountBadge badge)
        {
            if (icon == null) throw new ArgumentNullException(nameof(icon), "FluidAmountSlot always shows a fluid; the screen must not pass an empty one");

            // 白面は常に中身あり（液体の絵は必ずあり、欠けても名前の文字を描く）
            // The white face always has content (a fluid picture always exists, and a missing one draws the name)
            _frame.SetState(new SlotFrameState(false, true, false, false, false));
            _content.SetIcon(icon, GameIconFallback.Label);
            HoverTooltipMark.Mark(_frame, icon.Label);
            ApplyBadge();

            #region Internal

            void ApplyBadge()
            {
                if (!(badge is ShownSlotAmountBadge shown))
                {
                    _amountBand.RemoveFromHierarchy();
                    return;
                }
                _amount.SetText(UiText.FromVerbatim(SlotAmountFormat.Format(shown.Amount)));
                if (_amountBand.parent == null) _frame.Add(_amountBand);
            }

            #endregion
        }
    }
}
