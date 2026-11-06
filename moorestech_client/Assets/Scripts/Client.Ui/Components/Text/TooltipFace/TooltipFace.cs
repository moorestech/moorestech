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
        // USS の .tooltip-face__line の min-height と同じ 1 行の高さ（Web の継承 line-height 1.55 × 18px）
        // One-line height equal to the min-height of .tooltip-face__line in USS (Web inherited line-height 1.55 x 18px)
        private const float OneLineHeight = 27.9f;
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
                Add(label);
            }
        }

        private void FillMaxWidthWhenWrapped()
        {
            // Unity は折り返した面を最長行の幅に縮めるが、Web は最大幅まで広がるので、折り返し中は最大幅に固定する
            // Unity shrinks a wrapped face to its longest line while Web fills the max width, so pin the max width while wrapped
            var wrapped = false;
            foreach (var line in Children())
            {
                if (line.layout.height > OneLineHeight * 1.5f) wrapped = true;
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
