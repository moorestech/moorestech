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
        // 行ごとの 1 行の自然幅と、最大幅を超える行の数。行の形が変わったときにその行だけ更新する
        // Natural one-line width per line and how many exceed the max width; updated per line when its shape changes
        private readonly Dictionary<LocalizedLabel, float> _naturalWidthByLine = new();
        private int _linesOverMaxWidth;
        private float _judgedInnerMaxWidth = float.NaN;

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
            _naturalWidthByLine.Clear();
            _linesOverMaxWidth = 0;
            foreach (var line in lines)
            {
                var label = new LocalizedLabel { name = _lineName, pickingMode = PickingMode.Ignore };
                label.AddToClassList("tooltip-face__line");
                label.SetText(line);
                // 文が変わると行の形が変わるので、行側でも折り返しを判定し直す（面の幅が 320 に固定済みだと面の GeometryChanged は来ない）
                // A new sentence reshapes the line, so re-judge wrapping from the line too (a face already pinned to 320 gets no GeometryChanged)
                label.RegisterCallback<GeometryChangedEvent>(_ => RemeasureLine(label));
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

        // 形が変わった行だけを測り直す。全行を測り直すと、行数ぶんの通知×行数ぶんの計測で行数の 2 乗になる（数百行のツールチップで数秒固まった）
        // Re-measure only the line whose shape changed; re-measuring every line per notification is quadratic in the line count (froze for seconds on a tooltip of hundreds of lines)
        private void RemeasureLine(LocalizedLabel label)
        {
            // silent-ok: SetLines で外した行の遅れた通知は数えない / 行は面から外れており表示に影響しない
            if (label.parent != this) return;
            var naturalWidth = label.MeasureSingleLineWidth();
            if (TryResolveInnerMaxWidth(out var innerMaxWidth) && innerMaxWidth == _judgedInnerMaxWidth)
            {
                // 最大幅が前回の判定と同じなら、この行の超過の有無だけを差し替える
                // With the same max width as the last judgement, swap only this line's over-width state
                var wasOver = _naturalWidthByLine.TryGetValue(label, out var previousWidth) && innerMaxWidth < previousWidth;
                _naturalWidthByLine[label] = naturalWidth;
                _linesOverMaxWidth += (innerMaxWidth < naturalWidth ? 1 : 0) - (wasOver ? 1 : 0);
                EnableInClassList("tooltip-face--wrapped", 0 < _linesOverMaxWidth);
                return;
            }
            _naturalWidthByLine[label] = naturalWidth;
            FillMaxWidthWhenWrapped();
        }

        private void FillMaxWidthWhenWrapped()
        {
            // Unity は折り返した面を最長行の幅に縮めるが、Web は最大幅まで広がる。1 行の自然幅が最大幅の内側を超える行があれば最大幅に固定する
            // Unity shrinks a wrapped face to its longest line while Web fills the max width, so pin the max width when any line's one-line width exceeds the inner max width
            // silent-ok: 最大幅が解けない理由は TryResolveInnerMaxWidth が LogError で出す
            if (!TryResolveInnerMaxWidth(out var innerMaxWidth)) return;

            // 測り済みの自然幅を新しい最大幅と比べ直すだけで、文字幅は測らない
            // Only re-compare the measured natural widths against the new max width; no text is measured here
            _judgedInnerMaxWidth = innerMaxWidth;
            _linesOverMaxWidth = 0;
            foreach (var naturalWidth in _naturalWidthByLine.Values)
            {
                if (innerMaxWidth < naturalWidth) _linesOverMaxWidth++;
            }
            EnableInClassList("tooltip-face--wrapped", 0 < _linesOverMaxWidth);
        }

        private bool TryResolveInnerMaxWidth(out float innerMaxWidth)
        {
            innerMaxWidth = 0f;
            // 最大幅が長さで解けていなければ折り返しを判定できないので、理由を出して判定しない
            // Without a resolved max width wrapping cannot be judged, so report why and skip
            var maxWidthStyle = resolvedStyle.maxWidth;
            // 面と全行の GeometryChanged で何度も来るので、解けるまでは 1 回だけ出す
            // Face and line GeometryChanged repeat, so report once until it resolves
            if (maxWidthStyle.keyword != StyleKeyword.Undefined || float.IsNaN(maxWidthStyle.value))
            {
                if (!_unresolvedMaxWidthReported) Debug.LogError($"[TooltipFace] max-width is not a resolved length (keyword {maxWidthStyle.keyword}, value {maxWidthStyle.value}); check --tooltip-max-width: {name}");
                _unresolvedMaxWidthReported = true;
                return false;
            }
            _unresolvedMaxWidthReported = false;
            innerMaxWidth = maxWidthStyle.value - resolvedStyle.paddingLeft - resolvedStyle.paddingRight - resolvedStyle.borderLeftWidth - resolvedStyle.borderRightWidth;
            return true;
        }
    }
}
