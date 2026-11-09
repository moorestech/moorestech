using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 帯状の進捗ゲージ（名前に反し矢印形でない）
    /// Bar-shaped progress gauge (not arrow-shaped despite the name)
    /// </summary>
    public sealed class ProgressArrowBar : VisualElement
    {
        // Web の部品が固定で付ける testid。液体の行と採掘機が同じ名前で照準する
        // The testid the Web component hard-codes; the fluid row and the miner aim at the same name
        private const string FixedTestid = "progress-arrow-bar";

        private readonly VisualElement _fill;

        public ProgressArrowBar()
        {
            name = FixedTestid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("progress-arrow-bar");

            // 線は充填より先に置き、充填が内側の線を覆う Web の重なりにそろえる
            // The outline goes before the fill so the fill covers the inset line, matching the Web layering
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.AddToClassList("progress-arrow-bar__outline");
            Add(outline);
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("progress-arrow-bar__fill");
            Add(_fill);

            SetValue(0f);
        }

        public void SetValue(float ratio01)
        {
            _fill.style.width = Length.Percent(GaugeRatio.Clamp01(ratio01) * 100f);
        }
    }
}
