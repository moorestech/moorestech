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
    /// 表示文は自動で折り返さないので、表示文の間に配置へ返す幅と、測る幅（幅を内容で決めるラベルは受けた制約の幅）は LineEndPunctuationLayoutWidth が決める。測ったときと違う幅の制約が来たら（親の幅が変わった）測り直す。
    /// The visible label draws the source with normal wrapping until measured, then swaps in the display text (the measuring text is never drawn there); it re-measures only when the source, prefix, width or text style changes.
    /// The display text does not auto-wrap, so LineEndPunctuationLayoutWidth decides the width reported to layout and the width to measure at (the constraint width for content-sized labels); an unseen width constraint (the parent resized) triggers a re-measure.
    /// </remarks>
    internal class LineEndPunctuationTrimmer
    {
        private const int TicksBeforeReading = 2;

        private readonly Label _label;
        private readonly LineEndPunctuationLayoutWidth _layoutWidth = new();
        private LineEndPunctuationMeasurer _measurer;
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
            _label.RegisterCallback<CustomStyleResolvedEvent>(_ => OnStyleResolved());
            _label.RegisterCallback<DetachFromPanelEvent>(_ => OnDetachedFromPanel());
        }

        // 前置き（行送りの印）と原文を受ける。同じ入力なら進行中の測りも出し終えた表示文も壊さない
        // Take the prefix (line-height tag) and source; the same input keeps an ongoing measurement or the shown display text
        public void Show(string prefix, string source)
        {
            if (prefix == _prefix && source == _source) return;
            _prefix = prefix;
            _source = source;
            _layoutWidth.ForgetSource();
            Restart();
        }

        // 原文の間は測りを覚え、表示文の間は配置の幅を LineEndPunctuationLayoutWidth に決めさせる
        // Remember measures while the source is drawn; while the display text is shown let LineEndPunctuationLayoutWidth decide the layout width
        public Vector2 ToSourceSize(Vector2 measured, float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            if (_phase == TrimPhase.Plain) return measured;
            if (_phase != TrimPhase.Displayed)
            {
                _layoutWidth.RecordSource(measured, desiredWidth, widthMode);
                return measured;
            }
            var size = _layoutWidth.ForDisplayed(measured, desiredWidth, widthMode, out var needsRestart);
            if (!needsRestart) return size;
            // 測ったときと違う制約（親の幅が変わった）。配置の最中は文を替えられないので次の刻みで原文に戻して測り直す
            // A constraint unlike the measured one (the parent resized); text cannot change mid-layout, so restart on the next tick
            OnNextTickIfCurrent(Restart);
            return size;
        }

        private void Restart()
        {
            _generation++;
            _measurer?.End();
            RestoreInlineWhiteSpace();
            _layoutWidth.ForgetConstraints();
            _label.text = _prefix + _source;
            _phase = LineEndPunctuationTrim.CanTrim(_source) ? TrimPhase.WaitingForLayout : TrimPhase.Plain;
            if (_phase != TrimPhase.WaitingForLayout) return;

            // 原文の配置（DoMeasure が受ける制約）を見てから測り始める。大きさが変わらず GeometryChangedEvent が来ない場合は次の刻みで始める
            // Start after the source's layout (the constraint DoMeasure receives); when the size is unchanged and no GeometryChangedEvent comes, start on the next tick
            OnNextTickIfCurrent(() =>
            {
                // silent-ok: 先に GeometryChangedEvent で測り始めていたら重ねない / 測り始めた側が読む刻みを積む
                if (_phase == TrimPhase.WaitingForLayout) TryBeginMeasuring();
            });
        }

        private void OnNextTickIfCurrent(System.Action action)
        {
            var generation = _generation;
            // silent-ok: 待つ間に入力が替わった・別の理由で測り直したら古い予約は捨てる / 新しい世代の側が自分の予約を積む
            _label.schedule.Execute(() => { if (generation == _generation) action(); });
        }

        private void TryBeginMeasuring()
        {
            var width = _label.contentRect.width;
            // silent-ok: パネルに載って幅が決まるまで原文を普通に折り返して描く / 載った後の GeometryChangedEvent でここへ戻る
            if (_label.panel == null || !(width > 0f)) return;

            _phase = TrimPhase.Measuring;
            _measuredWidth = _layoutWidth.ChooseMeasureWidth(_label);
            _measuredStyle = LineEndTextStyle.Read(_label);
            // 測り役は詰める文を初めて測るときに作る（詰める文を持たないラベルには作らない）
            // Create the measurer on the first trimmable text (labels that never hold one never get it)
            _measurer ??= new LineEndPunctuationMeasurer();
            _measurer.Begin(_label, _prefix + LineEndPunctuationTrim.ToBreakMeasureText(_source), _measuredWidth, _measuredStyle);
            AfterTicks(TicksBeforeReading);
        }

        // 1 回目の刻みは測り役の配置より前に来うるので、2 回目の刻みで字の位置を読む
        // The first tick may precede the measurer's layout, so read glyph positions on the second
        private void AfterTicks(int ticksLeft)
        {
            OnNextTickIfCurrent(() =>
            {
                if (ticksLeft > 1) AfterTicks(ticksLeft - 1);
                else ApplyMeasuredBreaks();
            });
        }

        private void ApplyMeasuredBreaks()
        {
            var lineEnds = _measurer.ReadLineEndIndices(LineEndPunctuationTrim.CountPresentedGlyphs(_source));
            _measurer.End();
            // silent-ok: normal 以外の文は原文のまま（nowrap は行末で折れず、pre-wrap は詰めない） / pre-wrap の差は台帳 LX-180 が例外として持つ
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
            var widthChanged = _layoutWidth.NeedsRemeasureForBoxWidth(width, _measuredWidth);
            if (!widthChanged && LineEndTextStyle.Read(_label).SameAs(_measuredStyle)) return;
            Restart();
        }

        // 幅も高さも変わらない字のスタイルの変化（クラスの付け替えで字間だけ変わる等）は GeometryChangedEvent が来ないので、スタイルの解決で拾う
        // A text style change that keeps the size (e.g. a class swap changing only letter spacing) sends no GeometryChangedEvent, so catch it on style resolution
        private void OnStyleResolved()
        {
            // silent-ok: 測る前・詰めない文は比べる写しが無い / 測り始めるときに今のスタイルを写す
            if (_phase != TrimPhase.Measuring && _phase != TrimPhase.Displayed) return;
            if (LineEndTextStyle.Read(_label).SameAs(_measuredStyle)) return;
            Restart();
        }

        // 測る途中で外されたら測り役を畳み、載り直した後の配置で測り直す
        // Detached mid-measure: fold the measurer and measure again after the next layout on a panel
        private void OnDetachedFromPanel()
        {
            if (_phase != TrimPhase.Measuring) return;
            _generation++;
            _measurer?.EndLater();
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
