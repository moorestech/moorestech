using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 折り返す文の箱が狭すぎて UITK TextCore が固まる間だけ折り返しを止め、足りる幅に戻ったら折り返しへ戻す
    /// Stops a wrapping text's wrap only while its box is too narrow for UITK TextCore to wrap without freezing, and restores it once the width suffices
    /// </summary>
    /// <remarks>
    /// 固まるのは測り（DoMeasure）と描画の両方なので、測りは WrapSafeLabel が狭い幅で折り返さずに測り、描画は箱の幅でここが nowrap を当てる（配置の後の GeometryChangedEvent で当てた inline は同じフレームの配置のやり直しに入り、描画より先に効く）。
    /// 箱の幅を読めるのは配置の後なので、新しく組んだ・非表示から戻った・外された後の最初の測りは受けた制約の幅で決める。文やスタイルだけが変わり箱が変わらないときは測りで今の箱の幅を使う。
    /// 止めた・戻したときだけ開発者向けのログを出す（毎フレームは出さない）。設計書 A-224・台帳 LX-184
    /// Both measuring (DoMeasure) and rendering freeze, so WrapSafeLabel measures unwrapped at narrow widths and this guard applies nowrap by box width for rendering (inline set in GeometryChangedEvent joins the same frame's layout pass and lands before rendering).
    /// The box width is only known after layout, so the first measure of a new, re-shown or re-attached label decides from the constraint width; a text or style change that keeps the box uses the current box width.
    /// It logs for developers only when it starts or stops holding (never per frame). Design A-224, ledger LX-184
    /// </remarks>
    public class NarrowWrapGuard
    {
        private const int TextExcerptLength = 24;

        private readonly TextElement _element;
        private readonly InlineWhiteSpaceHolds _whiteSpace;
        private bool _boxKnown;
        // 測りの中で contentRect を読むと、字形の atlas を空にした後の描画で UITK の文字のジョブが壊れた（D-17 の全件の実測）。箱の幅は配置の後に写して使う
        // Reading contentRect inside a measure broke UITK's text jobs after the glyph atlas was emptied (D-17 full-suite runs); copy the box width after layout instead
        private float _boxWidth;

        public NarrowWrapGuard(TextElement element, InlineWhiteSpaceHolds whiteSpace)
        {
            _element = element;
            _whiteSpace = whiteSpace;
            _element.RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
            _element.RegisterCallback<DetachFromPanelEvent>(_ => _boxKnown = false);
        }

        // 今の文とスタイルで折り返しても固まらない最小の内容の幅（固まる並びが無ければ 0）
        // Narrowest content width that wraps the current text and style without freezing (0 without a freezing sequence)
        public float MinimumWrapWidth()
        {
            var resolved = _element.resolvedStyle;
            return NarrowWrapWidth.Of(_element.text, _element.enableRichText, resolved.fontSize, resolved.letterSpacing);
        }

        // 測る幅。今の文が折り返す（止めていない）のに制約が必要な幅より狭ければ必要な幅で測る（その幅で折り返すと固まる）
        // Width to measure at: when the text currently wraps (not held) and the constraint is under the needed width, measure at the needed width (wrapping narrower freezes)
        public float SafeMeasureWidth(float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            if (widthMode == VisualElement.MeasureMode.Undefined) return desiredWidth;
            var whiteSpace = _element.resolvedStyle.whiteSpace;
            if (whiteSpace != WhiteSpace.Normal && whiteSpace != WhiteSpace.PreWrap) return desiredWidth;
            // 比較で書く（Mathf.Max は NaN の制約を 0 に変えてしまう）
            // Compare instead of Mathf.Max, which turns a NaN constraint into 0
            var minimumWidth = MinimumWrapWidth();
            return desiredWidth < minimumWidth ? minimumWidth : desiredWidth;
        }

        // 測りの前に描く幅を見積もって止めるか決める。箱が分かっていれば今の箱、分からなければ受けた制約の幅
        // Decide the hold before measuring from the expected render width: the current box when known, otherwise the constraint width
        public void EvaluateBeforeMeasure(float desiredWidth, VisualElement.MeasureMode widthMode)
        {
            if (_boxKnown)
            {
                Evaluate(_boxWidth);
                return;
            }
            // silent-ok: 制約の無い測りは幅が決まらない / 配置の後の GeometryChangedEvent が箱の幅で決める
            if (widthMode == VisualElement.MeasureMode.Undefined) return;
            Evaluate(desiredWidth);
        }

        private void OnGeometryChanged()
        {
            // 非表示の祖先の下の幅 0 は配置も描画もされないので止めない。戻ったときの最初の測りは制約の幅で決める
            // Width 0 under a hidden ancestor is neither laid out nor rendered, so do not hold; the first measure after re-showing decides by constraint
            if (IsUnderHiddenAncestor())
            {
                _boxKnown = false;
                return;
            }
            _boxKnown = true;
            _boxWidth = _element.contentRect.width;
            Evaluate(_boxWidth);
        }

        private void Evaluate(float renderWidth)
        {
            var minimumWidth = MinimumWrapWidth();
            var hold = _whiteSpace.WrapsWithout(WhiteSpaceHoldReason.NarrowWidth) && renderWidth < minimumWidth;
            if (hold == _whiteSpace.IsHeld(WhiteSpaceHoldReason.NarrowWidth)) return;

            // 止めた・戻したときだけ出す。止めている間の見た目は Chrome と違う（Chrome は狭くても折り返す）
            // Log only on the change; while held the look differs from Chrome (which wraps at any width)
            if (hold)
            {
                _whiteSpace.Hold(WhiteSpaceHoldReason.NarrowWidth);
                Debug.LogWarning($"[NarrowWrapGuard] '{_element.name}' stops wrapping: width {renderWidth:0.#}px < {minimumWidth:0.#}px needed to wrap \"{Excerpt()}\" without freezing UITK TextCore (bd moorestech-fcpzz, LX-184)"); // look-exception: LX-184
                return;
            }
            _whiteSpace.Release(WhiteSpaceHoldReason.NarrowWidth);
            Debug.Log($"[NarrowWrapGuard] '{_element.name}' wraps again: width {renderWidth:0.#}px, needs {minimumWidth:0.#}px for \"{Excerpt()}\"");
        }

        private bool IsUnderHiddenAncestor()
        {
            for (var element = (VisualElement)_element; element != null; element = element.hierarchy.parent)
            {
                if (element.resolvedStyle.display == DisplayStyle.None) return true;
            }
            return false;
        }

        private string Excerpt()
        {
            var text = _element.text;
            return text.Length <= TextExcerptLength ? text : text.Substring(0, TextExcerptLength) + "…";
        }
    }
}
