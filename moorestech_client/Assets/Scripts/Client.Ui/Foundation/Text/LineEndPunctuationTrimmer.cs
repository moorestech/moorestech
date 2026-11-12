using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// ラベルの文を行末の閉じ約物を詰めた折り返しで描く（測る文で折り返し位置を読み、改行を入れた文を自動の折り返し無しで描く）
    /// Draws a label's text wrapped with line-end closing punctuation trimmed (reads breaks from a measuring text, then draws the newline-split text without auto wrap)
    /// </summary>
    /// <remarks>
    /// 文が変わるか幅が変わるたびに測る文を描き、スケジューラの 2 回目の刻み（＝測る文の配置と描画の後）で字の位置を読んで表示文へ替える。1 回目の刻みは文を配置前に入れたとき配置より前に来るので待つ。
    /// 測る文と表示文は折り返しが同じなので高さは変わらない。表示文に替えて 2 刻み後の幅（行末の約物が半分はみ出す分を含む）を落ち着いた幅とし、それへの変化では測り直さない（測り直すと幅が往復する）。
    /// Each text or width change draws the measuring text, reads glyph positions on the second scheduler tick (after it is laid out and drawn) and swaps in the display text; the first tick precedes layout when the text was set before layout, so it waits.
    /// Both texts wrap the same, so the height does not change; the width two ticks after the swap (including the half mark overhanging at line end) is the settled width, and changes to it do not re-measure, which would make the width oscillate.
    /// </remarks>
    internal class LineEndPunctuationTrimmer
    {
        private const int TicksBeforeReading = 2;

        private readonly Label _label;
        private string _prefix = string.Empty;
        private string _source;
        private int _generation;
        private WaitPhase _phase = WaitPhase.None;
        private float _measuredWidth = float.NaN;
        private float _settledWidth = float.NaN;

        public LineEndPunctuationTrimmer(Label label)
        {
            _label = label;
            _label.RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
        }

        // 前置き（行送りの印）と原文を受け、閉じ約物があれば測る文から始める
        // Take the prefix (line-height tag) and source; start from the measuring text when it has closing marks
        public void Show(string prefix, string source)
        {
            _prefix = prefix;
            _source = source;
            _generation++;
            _phase = WaitPhase.None;
            _measuredWidth = float.NaN;
            _settledWidth = float.NaN;
            _label.style.whiteSpace = StyleKeyword.Null;

            if (!LineEndPunctuationTrim.ContainsClosingPunctuation(source))
            {
                _label.text = prefix + source;
                return;
            }
            // 原文の rich text の印と描かれる字の番号を確かに対応づけられないので詰めない
            // Rich-text tags in the source cannot be mapped to presented glyph indices reliably, so do not trim
            if (source.IndexOf('<') >= 0)
            {
                Debug.LogWarning($"[LineEndPunctuationTrimmer] rich text in the source; line-end punctuation is not trimmed: {_label.name} '{source}'");
                _label.text = prefix + source;
                return;
            }

            _label.text = prefix + LineEndPunctuationTrim.ToBreakMeasureText(source);
            WaitTicks(WaitPhase.Reading);
        }

        private void WaitTicks(WaitPhase phase)
        {
            _phase = phase;
            AfterTicks(_generation, TicksBeforeReading);
        }

        private void AfterTicks(int generation, int ticksLeft)
        {
            _label.schedule.Execute(() =>
            {
                // silent-ok: 待つ途中で文か幅が替わったら古い刻みは捨てる / 新しい Show が刻みを積み直す
                if (generation != _generation) return;
                if (ticksLeft > 1)
                {
                    AfterTicks(generation, ticksLeft - 1);
                    return;
                }
                if (_phase == WaitPhase.Reading) ApplyMeasuredBreaks();
                else SettleWidth();
            });
        }

        private void ApplyMeasuredBreaks()
        {
            // silent-ok: 自動で折り返さない指定の文は行末で折れないので原文のまま / 詰めは LineEndPunctuationTrimTest が normal で確かめる
            if (_label.resolvedStyle.whiteSpace != WhiteSpace.Normal)
            {
                _phase = WaitPhase.None;
                _label.text = _prefix + _source;
                return;
            }

            // 測った折り返しで改行を入れ、自動の折り返しを止めて行末の約物をはみ出させる
            // Put newlines at the measured breaks and stop auto wrap so line-end marks may overhang
            _measuredWidth = _label.layout.width;
            _label.text = _prefix + LineEndPunctuationTrim.Apply(_source, ReadLineEndIndices());
            _label.style.whiteSpace = WhiteSpace.NoWrap;
            WaitTicks(WaitPhase.Settling);
        }

        private void SettleWidth()
        {
            _phase = WaitPhase.None;
            _settledWidth = _label.layout.width;
        }

        private void OnGeometryChanged()
        {
            // silent-ok: 測る・落ち着くのを待つ間の幅の変化は刻みが拾う / 詰めない文は幅で変わらない
            if (_phase != WaitPhase.None || float.IsNaN(_measuredWidth)) return;

            // 外から幅が変わったときだけ測り直す（測った幅・落ち着いた幅のどちらでもない）
            // Re-measure only when the width changed from outside (neither the measured nor the settled width)
            var width = _label.layout.width;
            if (Mathf.Approximately(width, _measuredWidth) || Mathf.Approximately(width, _settledWidth)) return;
            Show(_prefix, _source);
        }

        // 字ごとの位置を読み、次の字が下の行へ移る字を行末とする（位置は選択可能のあいだだけ読める）
        // Read each glyph's position; a glyph whose next glyph moves down a line is a line end (positions are readable only while selectable)
        private List<int> ReadLineEndIndices()
        {
            var selection = _label.selection;
            var wasSelectable = selection.isSelectable;
            var wasFocusable = _label.focusable;
            selection.isSelectable = true;

            var lineEnds = new List<int>();
            selection.cursorIndex = 0;
            var previousY = selection.cursorPosition.y;
            for (var index = 1; index <= _source.Length; index++)
            {
                selection.cursorIndex = index;
                var y = selection.cursorPosition.y;
                if (y > previousY + 0.5f) lineEnds.Add(index - 1);
                previousY = y;
            }

            selection.cursorIndex = 0;
            selection.isSelectable = wasSelectable;
            _label.focusable = wasFocusable;
            return lineEnds;
        }

        private enum WaitPhase
        {
            None,
            Reading,
            Settling,
        }
    }
}
