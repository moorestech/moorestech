using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 測る文を見えない測り役のラベルに描かせて、行末の字の番号を読む
    /// Has an invisible measurer label lay out the measuring text and reads the line-end glyph indices
    /// </summary>
    /// <remarks>
    /// 測り役は測る間だけ同じパネルの根に絶対配置で載せ、ラベルと同じ幅と文字のスタイルで折り返させる。見えるラベルには測る文を一度も描かない（測る文は約物の字が 0.5em 左へずれる）。
    /// 字の位置は選択可能なラベルでしか読めないので、測り役は常に選択可能にする（見えるラベルの選択を触らない）。
    /// The measurer sits absolutely positioned on the same panel's root only while measuring and wraps at the label's width and text style; the visible label never draws the measuring text (whose marks are shifted 0.5em left).
    /// Glyph positions are readable only on a selectable label, so the measurer is always selectable (the visible label's selection is never touched).
    /// </remarks>
    internal class LineEndPunctuationMeasurer
    {
        private readonly Label _measurer = new();

        public LineEndPunctuationMeasurer()
        {
            _measurer.name = "line-end-punctuation-measurer";
            _measurer.ClearClassList();
            _measurer.pickingMode = PickingMode.Ignore;
            _measurer.focusable = false;
            _measurer.selection.isSelectable = true;

            // 見えず・場所を取らず・余白なしで、幅いっぱいに普通の折り返しをする
            // Invisible, out of flow, no box spacing, wrapping normally across its width
            var style = _measurer.style;
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.visibility = Visibility.Hidden;
            style.whiteSpace = WhiteSpace.Normal;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.borderLeftWidth = 0f;
            style.borderRightWidth = 0f;
        }

        public void Begin(Label label, string measureText, float width, LineEndTextStyle textStyle)
        {
            label.panel.visualTree.Add(_measurer);
            textStyle.ApplyTo(_measurer.style);
            _measurer.enableRichText = true;
            _measurer.style.width = width;
            _measurer.text = measureText;
        }

        // 字ごとの位置を読み、次の字が下の行へ移る字を行末とする
        // Read each glyph's position; a glyph whose next glyph moves down a line is a line end
        public List<int> ReadLineEndIndices(int glyphCount)
        {
            var selection = _measurer.selection;
            var lineEnds = new List<int>();
            selection.cursorIndex = 0;
            var previousY = selection.cursorPosition.y;
            for (var index = 1; index <= glyphCount; index++)
            {
                selection.cursorIndex = index;
                var y = selection.cursorPosition.y;
                if (y > previousY + 0.5f) lineEnds.Add(index - 1);
                previousY = y;
            }
            selection.cursorIndex = 0;
            return lineEnds;
        }

        // 文を空にして根から外す（載せたままにすると測り役の数だけ配置の手間が残る）
        // Empty the text and detach from the root (left attached, each measurer keeps costing layout)
        public void End()
        {
            _measurer.text = string.Empty;
            _measurer.RemoveFromHierarchy();
        }
    }
}
