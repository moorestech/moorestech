using System;
using System.Collections.Generic;

namespace Client.Ui.State.Modal
{
    /// <summary>
    /// 保留中モーダル要求の内容
    /// Contents of a pending modal request
    /// </summary>
    public class ModalRequest
    {
        public string Id;
        public string Title;
        public string Message;
        public string ButtonText;
        public ModalVariant Variant;
        public bool RequiresInput;
        // 本文の下に並べる項目名と値の行。無ければ空（null にしない）
        // Label-and-values rows under the message; empty when none (never null)
        public IReadOnlyList<ModalTableRow> Rows = Array.Empty<ModalTableRow>();
    }

    /// <summary>
    /// モーダルの表の1行。項目名と値は要求側が解いた文（題・本文と同じ）
    /// One modal table row; label and values are requester-resolved text like the title and message
    /// </summary>
    public sealed class ModalTableRow
    {
        public string Label { get; }
        public string Values { get; }

        public ModalTableRow(string label, string values)
        {
            Label = label;
            Values = values;
        }
    }

    /// <summary>
    /// モーダルの表示種別。web の "confirm" | "error" に対応
    /// Modal display kind; maps to the web's "confirm" | "error"
    /// </summary>
    public enum ModalVariant
    {
        Confirm,
        Error,
    }

    /// <summary>
    /// モーダルの結果。描く側に依らない2値
    /// Modal result; the two renderer-independent values
    /// </summary>
    public enum ModalResult
    {
        Confirm,
        Cancel,
    }
}
