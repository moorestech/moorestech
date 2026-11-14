using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 回転生成機の本文。縦の択一で出力モード、充足率のゲージ、消費電力（Web ElectricToGearInventory）
    /// Electric-to-gear body: vertical exclusive switch of output modes, fulfillment gauge, consumed power (Web ElectricToGearInventory)
    /// </summary>
    public sealed class ElectricToGearBodyView : VisualElement, ISpecialSectionRenderer<SpecialBlockDetailViewModel>
    {
        public const string CommandName = "electric_to_gear.set_output_mode";

        private readonly SpecialViewKit _kit;
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly VisualElement _modeHolder = new();
        private readonly GaugeBar _fulfillment = new("electric-to-gear-fulfillment");
        private readonly LocalizedLabel _consumed = SpecialViewParts.Label("electric-to-gear-consumed-power", UiTypographyClass.TextMd);
        private ModeSwitch<int> _modes;
        private ElectricToGearDetail _shownModes;

        public ElectricToGearBodyView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "electric-to-gear-view";
            AddToClassList("electric-to-gear-view");
            var fulfillmentLabel = SpecialViewParts.Label(null, UiTypographyClass.TextSm);
            fulfillmentLabel.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ElectricToGearFulfillment));
            _stack.AddItem(_modeHolder);
            _stack.AddItem(fulfillmentLabel);
            _stack.AddItem(_fulfillment);
            _stack.AddItem(_consumed);
            Add(_stack);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 詳細の未着は Web と同じく本文を空にする（Web は詳細無しで null）
            // Without detail the body stays empty, as the Web returns null
            var present = detail.TryGetElectricToGear(out var electricToGear);
            UiDisplay.SetShown(_stack, present);
            // silent-ok: 詳細の未着は本文を空のまま（Web と同じ） / 届かないままなら録画（R-12）の回転生成機の段で気づく
            if (!present) return;

            // 択一はモードの一覧が変わったときだけ作り直す（毎回作るとフォーカスと押下が切れる。D-21）
            // Rebuild the switch only when the mode list changes (rebuilding every time drops focus and presses. D-21)
            if (_shownModes == null || !_shownModes.HasSameModes(electricToGear)) RebuildModes(electricToGear);
            _shownModes = electricToGear;
            _modes.SetSelected(electricToGear.SelectedIndex);
            _fulfillment.SetValue((float)electricToGear.FulfillmentRate);
            _consumed.SetText(SpecialDetailTexts.ElectricToGearConsumedPower(electricToGear));

            #region Internal

            void RebuildModes(ElectricToGearDetail source)
            {
                var options = new List<ModeSwitchOption<int>>();
                for (var i = 0; i < source.OutputModes.Count; i++) options.Add(new ModeSwitchOption<int>($"electric-to-gear-mode-{i}", i, SpecialDetailTexts.ElectricToGearOutputMode(source.OutputModes[i])));
                _modeHolder.Clear();
                _modes = new ModeSwitch<int>("electric-to-gear-modes", ModeSwitchOrientation.Vertical, ModeSwitchRole.Group, options);
                _modes.OnOptionSelected.Subscribe(index => _kit.Run(CommandName, _kit.Commands.SetElectricToGearMode(index)));
                _modeHolder.Add(_modes);
            }

            #endregion
        }
    }
}
