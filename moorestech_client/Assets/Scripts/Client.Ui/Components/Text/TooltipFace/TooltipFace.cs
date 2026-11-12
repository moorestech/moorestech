using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// ツールチップの面と行
    /// Tooltip face and lines
    /// </summary>
    public sealed class TooltipFace : VisualElement, IHoverTooltipFace
    {
        private readonly string _lineName;
        private bool _unresolvedMaxWidthReported;

        public TooltipFace(string testid, TooltipFaceLook look)
        {
            name = testid;
            _lineName = $"{testid}-line";
            pickingMode = PickingMode.Ignore;
            AddToClassList("tooltip-face");
            AddToClassList(LookClass(look));
            RegisterCallback<GeometryChangedEvent>(_ => FillMaxWidthWhenWrapped());

            #region Internal

            string LookClass(TooltipFaceLook lookKind)
            {
                return lookKind switch
                {
                    TooltipFaceLook.Cursor => "tooltip-face--cursor",
                    TooltipFaceLook.Hover => "tooltip-face--hover",
                    _ => throw new ArgumentOutOfRangeException(nameof(lookKind), lookKind, null),
                };
            }

            #endregion
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

            // 行が 0 件だと行側の GeometryChanged が来ないので、ここで折り返しの固定を外す
            // With no lines no line GeometryChanged arrives, so release the wrapped pin here
            if (lines.Count == 0) EnableInClassList("tooltip-face--wrapped", false);
        }

        // ホバーの宿主（Foundation）が置き場所の箱へ自分を載せさせる
        // Lets the hover host (Foundation) place this face into its anchor box
        public void AttachTo(VisualElement parent)
        {
            parent.Add(this);
        }

        private void FillMaxWidthWhenWrapped()
        {
            // Unity は折り返した面を最長行の幅に縮めるが、Web は最大幅まで広がる。1 行の自然幅が最大幅の内側を超える行があれば最大幅に固定する
            // Unity shrinks a wrapped face to its longest line while Web fills the max width, so pin the max width when any line's one-line width exceeds the inner max width
            // 最大幅が長さで解けていなければ折り返しを判定できないので、理由を出して判定しない
            // Without a resolved max width wrapping cannot be judged, so report why and skip
            var maxWidthStyle = resolvedStyle.maxWidth;
            // 面と全行の GeometryChanged で何度も来るので、解けるまでは 1 回だけ出す
            // Face and line GeometryChanged repeat, so report once until it resolves
            if (maxWidthStyle.keyword != StyleKeyword.Undefined || float.IsNaN(maxWidthStyle.value))
            {
                if (!_unresolvedMaxWidthReported) Debug.LogError($"[TooltipFace] max-width is not a resolved length (keyword {maxWidthStyle.keyword}, value {maxWidthStyle.value}); check --tooltip-max-width: {name}");
                _unresolvedMaxWidthReported = true;
                return;
            }
            _unresolvedMaxWidthReported = false;
            var maxWidth = maxWidthStyle.value;
            var innerMaxWidth = maxWidth - resolvedStyle.paddingLeft - resolvedStyle.paddingRight - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
            var wrapped = false;
            foreach (var child in Children())
            {
                var label = (LocalizedLabel)child;
                // 表示文は行末の約物の詰めで改行を含むことがあるので、1 行の幅は原文で測る
                // The display text may hold trim newlines, so measure the one-line width from the source
                var naturalWidth = label.MeasureTextSize(label.SourceText ?? string.Empty, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
                if (innerMaxWidth < naturalWidth) wrapped = true;
            }
            EnableInClassList("tooltip-face--wrapped", wrapped);
        }
    }
}
