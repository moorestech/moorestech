using System;
using Client.Ui.Foundation.Icons;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 液体 1 マス。背面のフィル・絵・量・名前のツールチップ（Web の FluidSlot）
    /// One fluid cell: back fill, picture, amount and name tooltip (the Web FluidSlot)
    /// </summary>
    public sealed class FluidSlot : VisualElement
    {
        private const string FillShownStateClass = "fluid-slot--fill-shown";
        private const string AmountShownStateClass = "fluid-slot--amount-shown";
        private const string GhostStateClass = "fluid-slot--ghost";

        private readonly VisualElement _fill;
        private readonly GameIcon _icon;
        private readonly OutlinedCountLabel _amount;

        public FluidSlot(string testid)
        {
            name = testid;
            AddToClassList("fluid-slot");

            // フィル → 絵 → 量の順に重ね、量を最前面に残す（Web の子の順）
            // Stack fill, picture, then amount so the amount stays frontmost (the Web child order)
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("fluid-slot__fill");
            Add(_fill);
            _icon = new GameIcon();
            _icon.AddToClassList("fluid-slot__icon");
            Add(_icon);

            // 量は白字に黒縁（Web の .iconTextOutlineDark）。縁の色は置き場のクラスがつまみで黒にする
            // The amount is white with a dark outline (the Web .iconTextOutlineDark); the placement class turns the outline knob black
            _amount = new OutlinedCountLabel(this, "fluid-slot__amount", UiTypographyClass.TextBold);
        }

        public void SetFluid(FluidSlotContent content)
        {
            switch (content)
            {
                case EmptyFluidSlotContent:
                    ShowEmpty();
                    break;
                case GhostFluidSlotContent ghost:
                    ShowGhost(ghost);
                    break;
                case FilledFluidSlotContent filled:
                    ShowFilled(filled);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(content), content.GetType().Name, "FluidSlotContent subtypes are closed; a new one must be drawn here");
            }

            #region Internal

            void ShowEmpty()
            {
                // 空タンクは素の枠だけ。ツールチップの印も外す（Web は Tooltip ごと描かない）
                // An empty tank is the bare frame only; the tooltip mark goes too (the Web renders no Tooltip)
                EnableInClassList(GhostStateClass, false);
                EnableInClassList(FillShownStateClass, false);
                EnableInClassList(AmountShownStateClass, false);
                _icon.ClearIcon();
                HoverTooltipMark.Unmark(this);
            }

            void ShowGhost(GhostFluidSlotContent ghost)
            {
                // フィル無しで絵と必要量だけを透かす
                // Fade only the picture and required amount, without a fill
                EnableInClassList(GhostStateClass, true);
                EnableInClassList(FillShownStateClass, false);
                ShowPictureAndAmount(ghost.Icon, ghost.Amount);
            }

            void ShowFilled(FilledFluidSlotContent filled)
            {
                // 高さと色は Web と同じく動的な値をインラインで当てる
                // Height and color are dynamic values applied inline, as on the Web
                EnableInClassList(GhostStateClass, false);
                EnableInClassList(FillShownStateClass, true);
                _fill.style.height = Length.Percent(FluidFillRatio.Of(filled.Amount, filled.Capacity) * 100f);
                _fill.style.backgroundColor = filled.Face;
                ShowPictureAndAmount(filled.Icon, filled.Amount);
            }

            void ShowPictureAndAmount(UiIcon icon, double amount)
            {
                // 絵が欠けたら何も代わりに出さない（フィルが残るので。Web の fallback none）。量の字は NaN でも隠さない
                // A missing picture shows no substitute, since the fill remains (the Web fallback none); the amount text is never hidden, even for NaN
                _icon.SetIcon(icon, GameIconFallback.None);
                _amount.SetText(UiText.FromVerbatim(SlotAmountFormat.Format(amount)));
                EnableInClassList(AmountShownStateClass, true);
                HoverTooltipMark.Mark(this, icon.Label);
            }

            #endregion
        }
    }
}
