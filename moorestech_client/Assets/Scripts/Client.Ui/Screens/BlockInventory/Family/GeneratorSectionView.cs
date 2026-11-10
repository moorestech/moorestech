using Client.Localization;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// 発電機の区画（Web GeneratorSection）。残燃料の棒 → 稼働率の文
    /// The generator section (Web GeneratorSection): remaining-fuel bar → operating-rate text
    /// </summary>
    public sealed class GeneratorSectionView : IDetailSectionView<GeneratorDetail>
    {
        private readonly UiStack _root = new(UiGap.Xs) { name = "generator-section" };
        private readonly GaugeBar _fuel = new("generator-fuel-progress");
        private readonly LocalizedLabel _operatingRate = new() { name = "generator-operating-rate" };

        public VisualElement Root => _root;

        public GeneratorSectionView()
        {
            _operatingRate.AddToClassList(UiTypographyClass.TextSm);
            _operatingRate.AddToClassList(UiTypographyClass.TextDefault);
            _root.AddItem(_fuel);
            _root.AddItem(_operatingRate);

            // 発電機でないブロックでは描かれないまま載るので、文の無いラベルが LogError にならないよう文を先に入れる
            // On non-generator blocks this is mounted without ever being drawn, so seed the text to keep the text-less label from logging an error
            ShowOperatingRate(0);
        }

        public void Render(GeneratorDetail detail)
        {
            _fuel.SetValue((float)BlockDetailMath.FuelRatio(detail.RemainingFuelTime, detail.CurrentFuelTime));
            ShowOperatingRate(detail.OperatingRate);
        }

        private void ShowOperatingRate(double operatingRate)
        {
            // 百分率は JS の Math.round と同じく .5 を上へ丸める
            // The percentage rounds .5 up like JS Math.round
            var percent = UiNumberFormat.RoundHalfUp(operatingRate * 100);
            _operatingRate.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.GeneratorOperatingRate, new[] { new LocalizationTextParam("rate", UiNumberFormat.Integer(percent)) }));
        }
    }
}
