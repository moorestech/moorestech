using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// ツールチップの面と行（Web の shared/ui/HoverTooltip・shared/tooltip/CursorTooltip の面）。位置と出し入れは置く側が持つ
    /// Tooltip face and lines (the face of Web shared/ui/HoverTooltip and shared/tooltip/CursorTooltip); placement and visibility belong to the host
    /// </summary>
    public sealed class TooltipFace : VisualElement
    {
        private readonly string _lineName;

        public TooltipFace(string testid, TooltipFaceLook look)
        {
            name = testid;
            _lineName = $"{testid}-line";
            pickingMode = PickingMode.Ignore;
            AddToClassList("tooltip-face");
            AddToClassList(LookClass(look));
            RegisterCallback<GeometryChangedEvent>(_ => FillMaxWidthWhenWrapped());
        }

        public void SetText(UiText text)
        {
            SetLines(new[] { text });
        }

        public void SetLines(IReadOnlyList<UiText> lines)
        {
            // 行ごとに 1 ラベル。Web の CursorTooltip も行ごとに testid を付ける
            // One label per line; the Web CursorTooltip also tags each line with a testid
            Clear();
            foreach (var line in lines)
            {
                var label = new LocalizedLabel { name = _lineName, pickingMode = PickingMode.Ignore };
                label.AddToClassList("tooltip-face__line");
                label.SetText(line);
                // 文が変わると行の形が変わるので、行側でも折り返しを判定し直す（面の幅が 320 に固定済みだと面の GeometryChanged は来ない）
                // A new sentence reshapes the line, so re-judge wrapping from the line too (a face already pinned to 320 gets no GeometryChanged)
                label.RegisterCallback<GeometryChangedEvent>(_ => FillMaxWidthWhenWrapped());
                Add(label);
            }
        }

        private void FillMaxWidthWhenWrapped()
        {
            // Unity は折り返した面を最長行の幅に縮めるが、Web は最大幅まで広がる。1 行の自然幅が最大幅の内側を超える行があれば最大幅に固定する
            // Unity shrinks a wrapped face to its longest line while Web fills the max width, so pin the max width when any line's one-line width exceeds the inner max width
            var maxWidth = resolvedStyle.maxWidth.value;
            var innerMaxWidth = maxWidth - resolvedStyle.paddingLeft - resolvedStyle.paddingRight - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
            var wrapped = false;
            foreach (var child in Children())
            {
                var label = (LocalizedLabel)child;
                var naturalWidth = label.MeasureTextSize(label.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
                if (naturalWidth > innerMaxWidth) wrapped = true;
            }
            EnableInClassList("tooltip-face--wrapped", wrapped);
        }

        private static string LookClass(TooltipFaceLook look)
        {
            return look switch
            {
                TooltipFaceLook.Cursor => "tooltip-face--cursor",
                TooltipFaceLook.Hover => "tooltip-face--hover",
                _ => throw new ArgumentOutOfRangeException(nameof(look), look, null),
            };
        }
    }
}
