using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// ラベルの文を行末の閉じ約物を詰めた折り返しで描く（見えない測り役で折り返し位置を読み、改行を入れた文を自動の折り返し無しで描く）
    /// Draws a label's text wrapped with line-end closing punctuation trimmed (an invisible measurer reads the breaks, then the newline-split text is drawn without auto wrap)
    /// </summary>
    /// <remarks>
    /// 見えるラベルは測り終えるまで原文を普通の折り返しで描き、測り終えたら表示文に替える（測る文を見えるラベルに描かない）。測り直すのは原文・前置き・幅・文字のスタイルのどれかが変わったときだけ。
    /// 表示文は自動で折り返さないので、幅を内容で決めるラベルの幅は原文のときに測った幅を返し（DoMeasure）、原文で測っていない幅の制約が来たら（親の幅が変わった）測り直す。こうして表示文に替えても配置の幅は原文と同じに保ち、幅の往復を起こさない。
    /// The visible label draws the source with normal wrapping until measured, then swaps in the display text (the measuring text is never drawn there); it re-measures only when the source, prefix, width or text style changes.
    /// The display text does not auto-wrap, so a content-sized label reports the width measured with the source (DoMeasure) and re-measures when an unseen width constraint arrives (the parent resized); the layout width stays that of the source and never oscillates.
    /// </remarks>
    internal class LineEndPunctuationTrimmer
    {
        private const int TicksBeforeReading = 2;
        private const float WidthTolerance = 0.5f;

        private readonly Label _label;
        private readonly LineEndPunctuationMeasurer _measurer = new();
        private readonly Dictionary<(VisualElement.MeasureMode, float), float> _sourceWidths = new();
        private string _prefix;
        private string _source;
        private int _generation;
        private TrimPhase _phase = TrimPhase.Plain;
        private float _measuredWidth;
        private LineEndTextStyle _measuredStyle;
        private bool _wrapOverridden;
        private StyleEnum<WhiteSpace> _inlineWhiteSpaceBeforeOverride;

        public LineEndPunctuationTrimmer(Label label)
        {
            _label = label;
            _label.RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
            _label.RegisterCallback<DetachFromPanelEvent>(_ => OnDetachedFromPanel());
        }

        // 前置き（行送りの印）と原文を受ける。同じ入力なら進行中の測りも出し終えた表示文も壊さない
        // Take the prefix (line-height tag) and source; the same input keeps an ongoing measurement or the shown display text
        public void Show(string prefix, string source)
        {
            if (prefix == _prefix && source == _source) return;
            _prefix = prefix;
            _source = source;
            Restart();
        }

        // 表示文のときは原文で測った幅を返し、配置の幅を原文と同じに保つ
        // While showing the display text, report the width measured with the source so the layout width matches the source
        public Vector2 ToSourceSize(Vector2 measured, float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            if (widthMode == VisualElement.MeasureMode.Exactly || _phase == TrimPhase.Plain) return measured;
            // 幅の制約は 1/4px に丸めて引く（同じ配置から来る制約の浮動小数の揺れを同じとみなす）
            // Constraints are keyed at quarter-pixel precision so float noise from the same layout matches
            var constraint = (widthMode, widthMode == VisualElement.MeasureMode.Undefined ? 0f : Mathf.Round(desiredWidth * 4f));
            if (_phase != TrimPhase.Displayed)
            {
                _sourceWidths[constraint] = measured.x;
                return measured;
            }
            if (_sourceWidths.TryGetValue(constraint, out var sourceWidth)) return new Vector2(sourceWidth, measured.y);

            // 原文で測っていない制約（親の幅が変わった）。配置の最中は文を替えられないので次の刻みで原文に戻して測り直す
            // A constraint unseen with the source (the parent resized); text cannot change mid-layout, so restart on the next tick
            var generation = _generation;
            _label.schedule.Execute(() =>
            {
                // silent-ok: 待つ間に別の理由で測り直したら重ねない / 測り直した側が新しい制約で測る
                if (generation == _generation) Restart();
            });
            return measured;
        }

        private void Restart()
        {
            _generation++;
            _measurer.End();
            RestoreInlineWhiteSpace();
            _sourceWidths.Clear();
            _label.text = _prefix + _source;
            _phase = LineEndPunctuationTrim.CanTrim(_source) ? TrimPhase.WaitingForLayout : TrimPhase.Plain;
            if (_phase == TrimPhase.WaitingForLayout) TryBeginMeasuring();
        }

        private void TryBeginMeasuring()
        {
            var width = _label.contentRect.width;
            // silent-ok: パネルに載って幅が決まるまで原文を普通に折り返して描く / 載った後の GeometryChangedEvent でここへ戻る
            if (_label.panel == null || !(width > 0f)) return;

            _phase = TrimPhase.Measuring;
            _measuredWidth = width;
            _measuredStyle = LineEndTextStyle.Read(_label);
            _measurer.Begin(_label, _prefix + LineEndPunctuationTrim.ToBreakMeasureText(_source), width, _measuredStyle);
            AfterTicks(_generation, TicksBeforeReading);
        }

        // 1 回目の刻みは測り役の配置より前に来うるので、2 回目の刻みで字の位置を読む
        // The first tick may precede the measurer's layout, so read glyph positions on the second
        private void AfterTicks(int generation, int ticksLeft)
        {
            _label.schedule.Execute(() =>
            {
                // silent-ok: 待つ途中で入力が替わったら古い刻みは捨てる / 新しい Restart が刻みを積み直す
                if (generation != _generation) return;
                if (ticksLeft > 1)
                {
                    AfterTicks(generation, ticksLeft - 1);
                    return;
                }
                ApplyMeasuredBreaks();
            });
        }

        private void ApplyMeasuredBreaks()
        {
            var lineEnds = _measurer.ReadLineEndIndices(LineEndPunctuationTrim.CountPresentedGlyphs(_source));
            _measurer.End();
            // silent-ok: 自動で折り返さない指定の文は行末で折れないので原文のまま / 詰めは LineEndPunctuationTrimTest が normal で確かめる
            if (_label.resolvedStyle.whiteSpace != WhiteSpace.Normal)
            {
                _phase = TrimPhase.Plain;
                return;
            }

            // 測った折り返しで改行を入れ、自動の折り返しを止めて行末の約物をはみ出させる
            // Put newlines at the measured breaks and stop auto wrap so line-end marks may overhang
            _phase = TrimPhase.Displayed;
            _label.text = _prefix + LineEndPunctuationTrim.Apply(_source, lineEnds);
            _inlineWhiteSpaceBeforeOverride = _label.style.whiteSpace;
            _wrapOverridden = true;
            _label.style.whiteSpace = WhiteSpace.NoWrap;
        }

        // 自動の折り返しを止める前の inline の指定（多くは未指定）へ戻す
        // Restore the inline value from before auto wrap was stopped (usually unset)
        private void RestoreInlineWhiteSpace()
        {
            if (!_wrapOverridden) return;
            _label.style.whiteSpace = _inlineWhiteSpaceBeforeOverride;
            _wrapOverridden = false;
        }

        private void OnGeometryChanged()
        {
            if (_phase == TrimPhase.WaitingForLayout)
            {
                TryBeginMeasuring();
                return;
            }
            // silent-ok: 詰めない文は幅で変わらない / 詰める文に替われば Show が測り始める
            if (_phase == TrimPhase.Plain) return;

            // silent-ok: 非表示の祖先の下で幅 0 になった間は今の表示を保つ / 表示し直した幅が違えば次の GeometryChangedEvent で測り直す
            var width = _label.contentRect.width;
            if (!(width > 0f)) return;
            if (Mathf.Abs(width - _measuredWidth) <= WidthTolerance && LineEndTextStyle.Read(_label).SameAs(_measuredStyle)) return;
            Restart();
        }

        // 測る途中で外されたら測り役を畳み、載り直した後の配置で測り直す
        // Detached mid-measure: fold the measurer and measure again after the next layout on a panel
        private void OnDetachedFromPanel()
        {
            if (_phase != TrimPhase.Measuring) return;
            _generation++;
            _measurer.End();
            _phase = TrimPhase.WaitingForLayout;
        }

        private enum TrimPhase
        {
            Plain,
            WaitingForLayout,
            Measuring,
            Displayed,
        }
    }
}
