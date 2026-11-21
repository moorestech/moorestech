using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.Modal;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.CleanRoom
{
    /// <summary>
    /// 「失敗率の改善方法」のモーダルの本文と行（要求時の言語で解いた文）。取り直しで中身が変わったかを比べる
    /// The improvement modal's message and rows, resolved in the request-time language; compared to detect changes on refetch
    /// </summary>
    public sealed class CleanRoomYieldImprovementModalContent
    {
        public string Message { get; }
        public IReadOnlyList<ModalTableRow> Rows { get; }

        private CleanRoomYieldImprovementModalContent(string message, IReadOnlyList<ModalTableRow> rows)
        {
            Message = message;
            Rows = rows;
        }

        // 確率が在り改善の行が1つ以上あるときだけ組める（ボタンの出る条件と同じ）
        // Buildable only when odds are present with at least one improvement row (same as the button's condition)
        public static bool TryBuild(SectionContent<CleanRoomYieldOdds> entry, out CleanRoomYieldImprovementModalContent content)
        {
            content = null;
            // silent-ok: モーダルを組めない条件は呼び出し元に返す / 開く操作は警告し、取り直しは表示中のモーダルを閉じる
            if (!entry.TryGetPresent(out var odds) || !odds.HasImprovements) return false;

            // 「今: Lv1 42.3% / … / 失敗 50.5%」を本文に、改善ごとに項目名と差分を1行に
            // "Now: Lv1 42.3% / … / Failed 50.5%" as the message; one label and deltas per improvement
            var message = UiText.Join(": ", new[] { new UiText(LocalizationKeys.Ui.BlockInventory.CleanRoomYieldImprovementNow), UiText.Join(" / ", CleanRoomYieldImprovementLogic.CurrentValues(odds)) }).Resolve();
            var rows = new List<ModalTableRow>();
            foreach (var row in CleanRoomYieldImprovementLogic.Rows(odds)) rows.Add(new ModalTableRow(row.Label.Resolve(), UiText.Join(" / ", row.Values).Resolve()));
            content = new CleanRoomYieldImprovementModalContent(message, rows);
            return true;
        }

        // 解いた文で比べるので、値の変化も言語の切り替えも「違う」になる
        // Compares resolved text, so both value changes and language switches count as different
        public bool SameAs(CleanRoomYieldImprovementModalContent other)
        {
            // silent-ok: 中身の差は呼び出し元に返す / 変化した内容でモーダルを出し直す
            if (Message != other.Message || Rows.Count != other.Rows.Count) return false;
            for (var i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].Label != other.Rows[i].Label || Rows[i].Values != other.Rows[i].Values) return false;
            }
            return true;
        }
    }
}
