using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 0〜1 を描く読み取り専用の横ゲージ
    /// Read-only horizontal gauge for 0 to 1
    /// </summary>
    public sealed class GaugeBar : VisualElement
    {
        private const string SlotSizeProperty = "--gauge-bar-slot-size";

        // Web の height: calc(var(--slot-size) / 6)。USS に calc が無いので C# で割る
        // Web height: calc(var(--slot-size) / 6); USS has no calc, so C# divides
        private const float SlotSizeToHeight = 1f / 6f;

        private readonly VisualElement _fill;

        public GaugeBar(string testid)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("gauge-bar");

            // 線は充填より先に置き、充填が線を覆う Web の重なりにそろえる
            // The outline goes before the fill so the fill covers it, matching the Web layering
            var outline = new VisualElement { pickingMode = PickingMode.Ignore };
            outline.AddToClassList("gauge-bar__outline");
            Add(outline);
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList("gauge-bar__fill");
            Add(_fill);

            SetValue(0f);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        public void SetValue(float ratio01)
        {
            // NaN は 0 として描く（Web の Number.isNaN の分岐）
            // NaN draws as 0 (the Web's Number.isNaN branch)
            var clamped = float.IsNaN(ratio01) ? 0f : Mathf.Clamp01(ratio01);
            _fill.style.width = Length.Percent(clamped * 100f);
        }

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、高さは前の値のまま
            // When unreadable, UssCustomStyleReader logs an error and the height keeps its previous value
            if (!UssCustomStyleReader.TryReadPixels(customStyle, SlotSizeProperty, out var slotSize)) return;
            style.height = slotSize * SlotSizeToHeight;
        }
    }
}
