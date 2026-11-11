using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 回転発電機の区画。状態の行はホバーで計測時期を説明し、停止は不足トーン（Web GearToElectricSection）
    /// GearToElectric section; the state row explains measurement timing on hover and a stop uses the shortage tone (Web GearToElectricSection)
    /// </summary>
    public sealed class GearToElectricSectionView : VisualElement, ISpecialDetailRenderer
    {
        private readonly LackHighlightText _state = new("gear-to-electric-state", LackHighlightTextSize.Sm);
        private readonly LocalizedLabel _supplied = SpecialViewParts.Label("gear-to-electric-supplied-power", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);

        public GearToElectricSectionView()
        {
            name = "gear-to-electric-section";
            AddToClassList("special-section");
            SpecialViewParts.SeedEmptyText(_state);
            HoverTooltipMark.Mark(_state, new UiText(LocalizationKeys.Ui.BlockInventory.GearToElectricMeasurementTiming));
            var stack = new UiStack(UiGap.Px2);
            stack.AddItem(_state);
            stack.AddItem(_supplied);
            Add(stack);
        }

        public bool HasDetail(SpecialBlockDetailViewModel detail) => detail.TryGetGearToElectric(out _);

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // silent-ok: 詳細が無ければ Presenter が IsApplicable を偽にして親が隠す（Render は呼ばれない。本文の中では本文が HasDetail で隠す） / 区画の L1「詳細の前は何も出さない」で気づく
            if (!detail.TryGetGearToElectric(out var generator)) return;

            // 状態の文と不足トーンは L0 の表で決める
            // The state sentence and shortage tone come from the L0 table
            var display = BlockDetailStateDisplay.GearToElectric(generator.State);
            _state.Render(new UiText(display.LabelKey), display.Insufficient);
            _supplied.SetText(SpecialDetailTexts.GearToElectricSuppliedPower(generator));
        }
    }
}
