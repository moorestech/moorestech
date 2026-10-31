namespace Game.Block.Blocks.CleanRoom.Machine.YieldPreview
{
    /// <summary>
    ///     抽選の中身の照会結果。次の新規抽選に今の条件で使う値と、確定済みで出力の空きを待っている抽選に使った値を分けて持つ
    ///     Draw-detail query result, keeping apart the values the next new draw would use under current conditions and those a fixed draw waiting for output space used
    /// </summary>
    public sealed class CleanRoomYieldPreviewReport
    {
        public CleanRoomYieldPreviewResult Next { get; }

        // 出力待ちの確定済み抽選が無ければ null
        // null when no fixed draw is waiting for output space
        public CleanRoomYieldPreviewResult PendingDraw { get; }

        public CleanRoomYieldPreviewReport(CleanRoomYieldPreviewResult next, CleanRoomYieldPreviewResult pendingDraw)
        {
            Next = next;
            PendingDraw = pendingDraw;
        }
    }
}
