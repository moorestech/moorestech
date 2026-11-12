using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 行末の約物の詰めで、どの幅で折り返しを測るかと、表示文の間に配置へ返す幅を決める
    /// Decides the width to measure line breaks at and the width reported to layout while the trimmed display text is shown
    /// </summary>
    /// <remarks>
    /// 幅を内容で決めるラベル（AtMost の制約で測られ、原文の最長行に縮む）は、縮んだ幅でなく受けた制約の幅で測る。Chrome の fit-content は詰めた字で幅を決めるので、縮んだ幅で測ると詰めて入る行も入らない。
    /// 箱の幅が決まったラベルは箱の幅で測り、表示文の間も原文で測った幅を返す（表示文は自動で折り返さないので、そのまま測らせると幅が往復する）。
    /// A content-sized label (measured under an AtMost constraint and shrunk to the source's longest line) is measured at the constraint width, not the shrunk width: Chrome's fit-content sizes with the trimmed glyphs, so the shrunk width would refuse lines that fit once trimmed.
    /// A label with a decided box width is measured at that width and keeps reporting the source-measured width while the display text is shown (the display text does not auto-wrap, so measuring it directly would make the width oscillate).
    /// </remarks>
    internal class LineEndPunctuationLayoutWidth
    {
        private const float WidthTolerance = 0.5f;

        private readonly Dictionary<(VisualElement.MeasureMode, float), float> _sourceWidths = new();
        private bool _hasLastSourceMeasure;
        private (VisualElement.MeasureMode, float) _lastSourceKey;
        private float _lastSourceDesiredWidth;
        private float _lastSourceMeasuredWidth;
        private (VisualElement.MeasureMode, float) _measuredKey;
        private float _reportedDisplayWidth;

        // 受けた制約の幅で測っている（幅を内容で決めるラベル）。このときラベルの箱の幅の変化では測り直さない
        // Measuring at the constraint width (content-sized label); the label's own box width changes then do not trigger a re-measure
        public bool FollowsConstraint { get; private set; }

        // 原文が替わったら前の原文の幅を捨てる
        // A new source discards the widths measured with the previous one
        public void ForgetSource()
        {
            _sourceWidths.Clear();
            _hasLastSourceMeasure = false;
        }

        // 原文を描いている間の測りを覚える
        // Remember each measure taken while the source is drawn
        public void RecordSource(Vector2 measured, float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            var key = KeyOf(desiredWidth, widthMode);
            if (widthMode != VisualElement.MeasureMode.Exactly) _sourceWidths[key] = measured.x;
            _hasLastSourceMeasure = true;
            _lastSourceKey = key;
            _lastSourceDesiredWidth = desiredWidth;
            _lastSourceMeasuredWidth = measured.x;
        }

        // 最後の原文の測りが AtMost で、その結果がいまの幅なら制約の幅で、そうでなければ箱の幅で測る
        // Measure at the constraint when the last source measure was AtMost and produced the current width; otherwise at the box width
        public float ChooseMeasureWidth(float contentWidth)
        {
            FollowsConstraint = _hasLastSourceMeasure
                && _lastSourceKey.Item1 == VisualElement.MeasureMode.AtMost
                && Mathf.Abs(_lastSourceMeasuredWidth - contentWidth) <= WidthTolerance
                && _lastSourceDesiredWidth > contentWidth + WidthTolerance;
            if (!FollowsConstraint) return contentWidth;
            _measuredKey = _lastSourceKey;
            _reportedDisplayWidth = _lastSourceDesiredWidth;
            return _lastSourceDesiredWidth;
        }

        // 表示文の間に配置へ返す大きさ。測ったときと違う制約が来たら needsRestart を立てる（原文に戻して測り直す）
        // Size reported to layout while the display text is shown; an unseen constraint sets needsRestart (back to the source to re-measure)
        public Vector2 ForDisplayed(Vector2 measured, float desiredWidth, VisualElement.MeasureMode widthMode, out bool needsRestart)
        {
            needsRestart = false;
            var key = KeyOf(desiredWidth, widthMode);
            if (FollowsConstraint)
            {
                // 測った制約では詰めた行の幅（制約を超えない）を返す。行末の約物の空白の半分は Chrome と同じく箱の外へはみ出す
                // Under the measured constraint report the trimmed lines' width capped at the constraint; half of a line-end mark's space overhangs like Chrome
                if (key == _measuredKey)
                {
                    _reportedDisplayWidth = Mathf.Min(measured.x, desiredWidth);
                    return new Vector2(_reportedDisplayWidth, measured.y);
                }
                if (widthMode == VisualElement.MeasureMode.Undefined) return _sourceWidths.TryGetValue(key, out var maxContent) ? new Vector2(maxContent, measured.y) : measured;
                // 返した幅で高さを測り直す Exactly は同じ配置の続き
                // An Exactly pass at the reported width is the same layout re-measuring the height
                if (widthMode == VisualElement.MeasureMode.Exactly && Mathf.Abs(desiredWidth - _reportedDisplayWidth) <= WidthTolerance) return measured;
                needsRestart = true;
                return measured;
            }

            if (widthMode == VisualElement.MeasureMode.Exactly) return measured;
            if (_sourceWidths.TryGetValue(key, out var sourceWidth)) return new Vector2(sourceWidth, measured.y);
            needsRestart = true;
            return measured;
        }

        // 幅の制約は 1/4px に丸めて引く（同じ配置から来る制約の浮動小数の揺れを同じとみなす）
        // Constraints are keyed at quarter-pixel precision so float noise from the same layout matches
        private static (VisualElement.MeasureMode, float) KeyOf(float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            return (widthMode, widthMode == VisualElement.MeasureMode.Undefined ? 0f : Mathf.Round(desiredWidth * 4f));
        }
    }
}
