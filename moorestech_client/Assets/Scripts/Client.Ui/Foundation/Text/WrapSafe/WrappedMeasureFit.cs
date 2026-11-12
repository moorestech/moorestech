namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 固まらない幅で折り返して測った内容の幅を覚え、その幅以上の箱なら同じ改行で描けると答える
    /// Remembers the content width measured while wrapping at a non-freezing width and tells whether a box at least that wide draws the same line breaks
    /// </summary>
    /// <remarks>
    /// 内容幅の箱は折り返した最長の行まで縮むので、必要な幅（字間と 1px を広めに倒した見積もり）を下回りうる。測った最長の行が入る幅なら TextCore は同じ所で折り返し、連なりと次の字が入らない行は生じない。
    /// 文の参照と必要な幅（字の大きさ・字間）が同じ間だけ使い、どちらかが替われば無効にする（測りの無い固定の箱で古い記録を使わない）。設計書 A-224
    /// A content-sized box shrinks to its longest wrapped line and can fall under the needed width (an estimate erring wide by spacing and 1px); at a width holding that measured line TextCore breaks at the same places, so no line leaves a run without its next glyph.
    /// It applies only while the text reference and the needed width (font size, spacing) are unchanged, so a fixed box without measuring never reuses a stale record. Design A-224
    /// </remarks>
    internal class WrappedMeasureFit
    {
        private string _text;
        private float _minimumWidth;
        private float _measuredWidth;
        private bool _recorded;

        public void Record(string text, float minimumWidth, float measuredWidth)
        {
            _text = text;
            _minimumWidth = minimumWidth;
            _measuredWidth = measuredWidth;
            _recorded = true;
        }

        public void Clear()
        {
            _recorded = false;
            _text = null;
        }

        // 今の文と必要な幅で測った最長の行が、この幅に収まるか
        // Whether the longest line measured for this text and needed width fits this width
        public bool Fits(string text, float minimumWidth, float renderWidth)
        {
            return _recorded && ReferenceEquals(text, _text) && minimumWidth == _minimumWidth && renderWidth >= _measuredWidth;
        }
    }
}
