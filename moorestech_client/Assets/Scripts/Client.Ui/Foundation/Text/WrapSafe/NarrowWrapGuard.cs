using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 折り返す文の箱が狭すぎて UITK TextCore が固まる間だけ折り返しを止め、足りる幅に戻ったら折り返しへ戻す
    /// Stops a wrapping text's wrap only while its box is too narrow for UITK TextCore to wrap without freezing, and restores it once the width suffices
    /// </summary>
    /// <remarks>
    /// 固まるのは測り（DoMeasure）と描画の両方なので、測りは WrapSafeLabel が狭い幅で折り返さずに測り、描画は箱の幅でここが nowrap を当てる（配置の後の GeometryChangedEvent で当てた inline は同じフレームの配置のやり直しに入り、描画より先に効く）。
    /// 箱の幅を読めるのは配置の後なので、新しく組んだ・非表示から戻った・外された後と、文か字の大きさ・字間が替わった後の最初の測りは受けた制約の幅で決める。測りの来ない固定の箱（幅も高さも決まっている）は、文の差し替え（text の上書き）とスタイルの解決（CustomStyleResolvedEvent）の時に今の箱の幅で決める。
    /// 内容幅の箱は折り返した最長の行まで縮むので、固まらない幅で折り返して測った幅以上の箱は止めない（止めると 1 行で測り直して箱が戻り、止める／戻すを毎フレーム往復する）。止めた・戻したときだけ開発者向けのログを出す。設計書 A-224・台帳 LX-184
    /// Both measuring (DoMeasure) and rendering freeze, so WrapSafeLabel measures unwrapped at narrow widths and this guard applies nowrap by box width for rendering (inline set in GeometryChangedEvent joins the same frame's layout pass and lands before rendering).
    /// The box width is only known after layout, so the first measure of a new, re-shown or re-attached label, or after its text, font size or spacing changed, decides from the constraint; a fixed box (width and height set) gets no measure, so a text swap (the text override) or a style resolution (CustomStyleResolvedEvent) decides from the current box.
    /// A content-sized box shrinks to its longest wrapped line, so a box at least as wide as a wrap measured at a safe width is never held (holding re-measures one line, widens the box and toggles every frame). It logs for developers only when it starts or stops holding. Design A-224, ledger LX-184
    /// </remarks>
    internal class NarrowWrapGuard
    {
        private readonly TextElement _element;
        private readonly InlineWhiteSpaceHolds _whiteSpace;
        private readonly WrappedMeasureFit _wrappedMeasureFit = new();
        private bool _boxKnown;
        // 測りの中で配置の途中の contentRect を読まないよう、配置の後に写した箱の幅を使う
        // Use the box width copied after layout so a measure never reads a mid-layout contentRect
        private float _boxWidth;
        private bool _boxSizedByMeasure;
        private bool _measuredSinceGeometry;
        private bool _contentChangedSinceGeometry;
        private float _decidedMinimumWidth;
        private string _scannedText;
        private int _longestRun;

        public NarrowWrapGuard(TextElement element, InlineWhiteSpaceHolds whiteSpace)
        {
            _element = element;
            _whiteSpace = whiteSpace;
            _element.RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
            _element.RegisterCallback<CustomStyleResolvedEvent>(_ => OnStyleResolved());
            _element.RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        // 今の文とスタイルで折り返しても固まらない最小の内容の幅（固まる並びが無ければ 0）
        // Narrowest content width that wraps the current text and style without freezing (0 without a freezing sequence)
        public float MinimumWrapWidth()
        {
            // 連なりは文が替わったときだけ数え直す。固まる並びの無い文（大半）はスタイルを読まずに 0 を返す
            // Recount the run only when the text changes; texts without a freezing sequence (most) return 0 without reading style
            var text = _element.text;
            if (!ReferenceEquals(text, _scannedText))
            {
                _scannedText = text;
                _longestRun = NarrowWrapWidth.LongestRunBetweenGlyphs(text, _element.enableRichText);
            }
            if (_longestRun == 0) return 0f;
            var resolved = _element.resolvedStyle;
            return NarrowWrapWidth.OfRun(_longestRun, resolved.fontSize, resolved.letterSpacing);
        }

        // 測る幅。今の文が折り返す（止めていない）のに制約が必要な幅より狭ければ必要な幅で測る（その幅で折り返すと固まる）
        // Width to measure at: when the text currently wraps (not held) and the constraint is under the needed width, measure at the needed width (wrapping narrower freezes)
        public float SafeMeasureWidth(float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            if (widthMode == VisualElement.MeasureMode.Undefined) return desiredWidth;
            // 比較で書く（Mathf.Max は NaN の制約を 0 に変えてしまう）
            // Compare instead of Mathf.Max, which turns a NaN constraint into 0
            var minimumWidth = MinimumWrapWidth();
            if (!(desiredWidth < minimumWidth)) return desiredWidth;
            var whiteSpace = _element.resolvedStyle.whiteSpace;
            return whiteSpace == WhiteSpace.Normal || whiteSpace == WhiteSpace.PreWrap ? minimumWidth : desiredWidth;
        }

        // 測りの前に描く幅を見積もって止めるか決める。箱が今の文とスタイルのものなら今の箱、そうでなければ受けた制約の幅
        // Decide the hold before measuring from the expected render width: the current box when it belongs to the current text and style, otherwise the constraint
        public void EvaluateBeforeMeasure(float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            _measuredSinceGeometry = true;
            if (_boxKnown && !_contentChangedSinceGeometry)
            {
                Evaluate(_boxWidth);
                return;
            }
            // silent-ok: 制約の無い測りは幅が決まらない / 配置の後の GeometryChangedEvent が箱の幅で決める
            if (widthMode == VisualElement.MeasureMode.Undefined) return;
            // 上限だけの広い制約は最後の幅とは限らない（行の基準の測りの後で縮む）ので、それでは戻さず箱か配置の後に任せる
            // A wide at-most constraint need not be the final width (a row's basis measure shrinks afterwards), so it never releases; the box or the post-layout pass does
            var deferRelease = widthMode == VisualElement.MeasureMode.AtMost && _whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth) && !(desiredWidth < MinimumWrapWidth());
            // silent-ok: 止めたままにする / 箱が分かっていれば今ここで、無ければ配置の後の GeometryChangedEvent が箱の幅で戻す
            if (deferRelease && !_boxKnown) return;
            Evaluate(deferRelease ? _boxWidth : desiredWidth);
        }

        // 折り返して測った結果を覚える。固まらない幅（か制約なし）で折り返したときだけ、その最長の行の幅を箱の判定に使う
        // Remember a wrapped measure; only a wrap at a non-freezing width (or unconstrained) lends its longest line width to the box decision
        public void RecordMeasure(float measureWidth, VisualElement.MeasureMode widthMode, float measuredWidth)
        {
            var minimumWidth = MinimumWrapWidth();
            // silent-ok: 固まる並びの無い文と止めている文は箱を必要な幅で判定しない / 文が替われば次の測りでここへ来る
            if (minimumWidth <= 0f || _whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth) || !_whiteSpace.WrapsWithout(WhiteSpaceHoldReason.NarrowWidth)) return;
            if (widthMode != VisualElement.MeasureMode.Undefined && measureWidth < minimumWidth) return;
            _wrappedMeasureFit.Record(_element.text, minimumWidth, measuredWidth);
        }

        // 文が替わった。測りの来ない固定の箱は今の箱で決め、必要な幅が替わった測りの来る箱は次の測りが制約の幅で決める（行末の約物の詰めの差し替えのように同じ幅なら今の箱）
        // The text changed: a fixed box decides from the current box; a measured box whose needed width changed decides at the next measure from the constraint (an equal width, like a line-end trim swap, keeps the box)
        public void OnTextChanged()
        {
            if (MinimumWrapWidth() != _decidedMinimumWidth) _contentChangedSinceGeometry = true;
            EvaluateFixedBox();
        }

        private void OnStyleResolved()
        {
            // 必要な幅が替わらない解決（色や自分の nowrap の当て外しなど）は何もしない
            // Resolutions that keep the needed width (colours, this guard's own nowrap) do nothing
            // silent-ok: 字の大きさ・字間が替わらなければ判定も替わらない / 替われば必要な幅が変わってここを通る
            if (MinimumWrapWidth() == _decidedMinimumWidth) return;
            _contentChangedSinceGeometry = true;
            EvaluateFixedBox();
        }

        private void EvaluateFixedBox()
        {
            _decidedMinimumWidth = MinimumWrapWidth();
            // silent-ok: 箱がまだ無い・測りで大きさが決まる箱は次の測りが決める / 測りの中の EvaluateBeforeMeasure が受けた制約で判定する
            if (!_boxKnown || _boxSizedByMeasure) return;
            // silent-ok: 非表示の祖先の下は描かれない / 表示し直した後の GeometryChangedEvent が箱の幅で決める
            if ((_decidedMinimumWidth <= 0f && !_whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth)) || IsUnderHiddenAncestor()) return;
            Evaluate(_boxWidth);
        }

        private void OnGeometryChanged()
        {
            // この配置で測りが来なかった箱は幅も高さも決まっている（文やスタイルが替わっても次の測りは来ない）
            // A box laid out without a measure has a set width and height (a text or style change brings no measure)
            _boxSizedByMeasure = _measuredSinceGeometry;
            _measuredSinceGeometry = false;
            _contentChangedSinceGeometry = false;
            _boxKnown = true;
            _boxWidth = _element.contentRect.width;
            // 固まる並びの無い文は箱だけ写す。非表示の祖先の下の幅 0 は配置も描画もされないので止めない
            // Texts without a freezing sequence only copy the box; width 0 under a hidden ancestor is neither laid out nor drawn, so do not hold
            // silent-ok: 固まる並びの無い文は判定しない / 文が替われば OnTextChanged か次の測りで判定する
            if (MinimumWrapWidth() <= 0f && !_whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth))
            {
                _decidedMinimumWidth = 0f;
                return;
            }
            if (IsUnderHiddenAncestor())
            {
                _boxKnown = false;
                return;
            }
            Evaluate(_boxWidth);
        }

        private void OnDetached()
        {
            _boxKnown = false;
            _boxSizedByMeasure = false;
            _wrappedMeasureFit.Clear();
        }

        private void Evaluate(float renderWidth)
        {
            var minimumWidth = MinimumWrapWidth();
            _decidedMinimumWidth = minimumWidth;
            // silent-ok: 固まる並びが無く止めてもいない文は何もしない / 文が替われば OnTextChanged か次の測りでここへ来る
            if (minimumWidth <= 0f && !_whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth)) return;
            var tooNarrow = renderWidth < minimumWidth && !_wrappedMeasureFit.Fits(_element.text, minimumWidth, renderWidth);
            var hold = _whiteSpace.WrapsWithout(WhiteSpaceHoldReason.NarrowWidth) && tooNarrow;
            if (hold == _whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth)) return;

            // 止めた・戻したときだけ知らせる
            // Report only on the change
            if (hold)
            {
                _whiteSpace.Hold(WhiteSpaceHoldReason.NarrowWidth);
                NarrowWrapGuardLog.Held(_element, renderWidth, minimumWidth);
                return;
            }
            _whiteSpace.Release(WhiteSpaceHoldReason.NarrowWidth);
            NarrowWrapGuardLog.Released(_element, renderWidth, minimumWidth);
        }

        private bool IsUnderHiddenAncestor()
        {
            for (var element = (VisualElement)_element; element != null; element = element.hierarchy.parent)
            {
                if (element.resolvedStyle.display == DisplayStyle.None) return true;
            }
            return false;
        }
    }
}
