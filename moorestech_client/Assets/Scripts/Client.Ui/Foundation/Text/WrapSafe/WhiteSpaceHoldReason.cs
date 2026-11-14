using System;

namespace Client.Ui.Foundation.Text.WrapSafe
{
    /// <summary>
    /// 文の自動の折り返しを止めている理由
    /// Reasons for stopping a text's auto wrap
    /// </summary>
    [Flags]
    internal enum WhiteSpaceHoldReason
    {
        None = 0,
        // 測った改行入りの表示文を描画中
        // The line-end trim shows display text with its measured newlines
        LineEndTrim = 1,
        // 箱が狭く、折り返すと UITK TextCore が固まる（bd moorestech-fcpzz）
        // The box is too narrow to wrap without freezing UITK TextCore (bd moorestech-fcpzz)
        NarrowWidth = 2,
    }
}
