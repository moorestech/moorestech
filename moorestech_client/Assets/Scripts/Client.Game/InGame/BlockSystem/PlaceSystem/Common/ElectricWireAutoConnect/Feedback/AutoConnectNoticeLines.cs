using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect.Feedback
{
    /// <summary>
    /// 自動接続プレビューのカーソルセル状態から、積む案内行を決める純粋な判断部
    /// Pure judgement that maps the auto-connect preview's cursor-cell state to the notice line to push
    /// </summary>
    public static class AutoConnectNoticeLines
    {
        /// <summary>
        /// カーソルセルの案内行を積む。戻り値は「電線不足を積んだか」で、呼び出し元の線描画色の分岐に使う
        /// Pushes the cursor cell's notice line; the return value says whether wire shortage was pushed, which drives the caller's wire coloring
        /// </summary>
        public static bool Report(bool cursorWirePlaceable, int cursorRawTargetCount, bool hasOutOfRangeNeighbor, int totalCost, PlacementFeedback feedback)
        {
            // 電線不足は自動接続プレビューが唯一拒否する理由なので他の案内より優先する
            // Insufficient wire is the only rejection reason here, so it takes precedence over the other notices
            if (!cursorWirePlaceable)
            {
                feedback.Add(ElectricWireFeedbackLines.WireShortage());
                return true;
            }

            // 1件も配線されず、かつ範囲判定で落ちた近傍が実在するときだけ範囲外を案内する
            // Report out-of-range only when nothing gets wired and a neighbor actually failed the range check
            if (NeedsOutOfRangeProbe(cursorWirePlaceable, cursorRawTargetCount) && hasOutOfRangeNeighbor)
            {
                feedback.Add(ElectricWireFeedbackLines.WireOutOfRangeNotice());
                return false;
            }

            if (ElectricWireFeedbackLines.TryWireCost(totalCost, out var costLine)) feedback.Add(costLine);
            return false;
        }

        /// <summary>
        /// 範囲外案内の近傍走査が要るかを返す。走査は全ブロック走査で重く、呼び出し元は結果をこの判定で遅延させる
        /// Whether the out-of-range neighbor scan is needed; the scan walks every block, so callers defer it behind this judgement
        /// </summary>
        public static bool NeedsOutOfRangeProbe(bool cursorWirePlaceable, int cursorRawTargetCount)
        {
            return cursorWirePlaceable && cursorRawTargetCount == 0;
        }
    }
}
