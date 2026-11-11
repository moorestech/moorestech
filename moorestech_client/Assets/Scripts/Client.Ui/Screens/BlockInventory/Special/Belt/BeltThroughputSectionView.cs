using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.Text;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// ベルトの流量の区画の View。選んだ窓の行・計測済みなら分間・直近 9 秒の棒を描くだけで、購読も窓の選択も持たない（Web BeltThroughputSection の見た目）
    /// View of the belt throughput section; draws the selected window row, per-minute when measured and nine recent bars, holding no subscription or window choice (Web BeltThroughputSection look)
    /// </summary>
    public sealed class BeltThroughputSectionView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Px2);
        private readonly LocalizedLabel _window = SpecialViewParts.Label("belt-throughput-window", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly LocalizedLabel _perMinute = SpecialViewParts.Label("belt-throughput-per-minute", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly BarSeries _bars = new("belt-throughput-bars");

        public BeltThroughputSectionView()
        {
            name = "belt-throughput-section";
            AddToClassList("special-section");
            _stack.AddItem(_window);
            _stack.AddItem(_perMinute);
            _stack.AddItem(_bars);
            Add(_stack);
        }

        public void Render(BeltThroughputRowDisplay row, IReadOnlyList<float?> bars)
        {
            // 計測済みの窓だけ分間の行を出す
            // Show the per-minute row only for a measured window
            _window.SetText(row.Text);
            var measured = row as MeasuredBeltRowDisplay;
            _stack.SetItemShown(_perMinute, measured != null);
            if (measured != null) _perMinute.SetText(new UiText(LocalizationKeys.Ui.BlockInventory.ItemsPerMinute, new[] { new LocalizationTextParam("itemsPerMinute", measured.ItemsPerMinute) }));
            _bars.SetValues(bars);
        }
    }
}
