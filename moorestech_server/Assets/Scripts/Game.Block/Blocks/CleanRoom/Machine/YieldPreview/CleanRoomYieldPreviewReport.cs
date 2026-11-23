using System.Collections.Generic;

namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     抽選の中身の照会結果。役割なし・部屋が稼働を許さない・数字あり の種別ごとの型のどれか1つだけになり、数字は「数字あり」の型だけが必ず持つ
    ///     Draw-detail query result; always exactly one of the per-kind types (no role, room not operable, with odds), and only the with-odds type carries numbers, always
    /// </summary>
    public abstract class CleanRoomYieldPreviewReport
    {
        // 種別はこのファイルの3つの型に閉じる
        // The kinds are closed to the three types in this file
        private protected CleanRoomYieldPreviewReport()
        {
        }
    }

    // 未選択か、役割表に無いレシピ
    // No recipe selected, or a recipe outside the role tables
    public sealed class CleanRoomNoRoleYieldReport : CleanRoomYieldPreviewReport
    {
        public static readonly CleanRoomNoRoleYieldReport Instance = new();

        private CleanRoomNoRoleYieldReport()
        {
        }
    }

    // 部屋が稼働を許さない（未密閉・クラス不足）。停止理由は部屋の欄が出す
    // The room does not allow operation (not enclosed or class out); the room section shows the halt reason
    public sealed class CleanRoomNotOperableYieldReport : CleanRoomYieldPreviewReport
    {
        public static readonly CleanRoomNotOperableYieldReport Instance = new();

        private CleanRoomNotOperableYieldReport()
        {
        }
    }

    // 数字あり（EUV・ダイシング）。今の条件での最終の内訳と、改善の一覧（並べる順）を必ず持つ
    // With odds (EUV, dicing): always carries the final odds under current conditions and the improvement list in display order
    public sealed class CleanRoomOddsYieldReport : CleanRoomYieldPreviewReport
    {
        public CleanRoomFinalOdds Odds { get; }
        public IReadOnlyList<CleanRoomYieldImprovement> Improvements { get; }

        public CleanRoomOddsYieldReport(CleanRoomFinalOdds odds, IReadOnlyList<CleanRoomYieldImprovement> improvements)
        {
            Odds = odds;
            Improvements = improvements;
        }
    }
}
